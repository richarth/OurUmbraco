﻿using System;
using System.Text;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Mvc;
using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using OurUmbraco.Community.People;
using OurUmbraco.Our.Models;
using Skybrud.Social.GitHub.OAuth;
using Skybrud.Social.GitHub.Responses.Authentication;
using Skybrud.Social.GitHub.Responses.Users;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Web.Mvc;

namespace OurUmbraco.Our.Controllers
{
    public class ProfileController: SurfaceController
    {
        [ChildActionOnly]
        public ActionResult Render()
        {
            var memberService = Services.MemberService;
            var member = memberService.GetById(Members.GetCurrentMemberId());
            var avatarService = new AvatarService();
            var avatarPath = avatarService.GetMemberAvatar(member);
            var avatarHtml = avatarService.GetImgWithSrcSet(avatarPath, member.Name, 100);

            var profileModel = new ProfileModel
            {
                Name = member.Name,
                Email = member.Email,
                Bio = member.GetValue<string>("profileText"),
                Location = member.GetValue<string>("location"),
                Company = member.GetValue<string>("company"),
                TwitterAlias = member.GetValue<string>("twitter"),
                Avatar = avatarPath,
                AvatarHtml = avatarHtml,
                GitHubUsername = member.GetValue<string>("github"),

                Latitude = member.GetValue<string>("latitude"), //TODO: Parse/cleanup bad data - auto remove it for user & resave the member?
                Longitude = member.GetValue<string>("longitude")
            };

            return PartialView("~/Views/Partials/Members/Profile.cshtml", profileModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkGitHub() {

            string rootUrl = Request.Url.GetLeftPart(UriPartial.Authority);

            GitHubOAuthClient client = new GitHubOAuthClient();
            client.ClientId = WebConfigurationManager.AppSettings["GitHubClientId"];
            client.ClientSecret = WebConfigurationManager.AppSettings["GitHubClientSecret"];
            client.RedirectUri = rootUrl + "/umbraco/surface/Profile/LinkGitHub";

            // Set the state (a unique/random value)
            string state = Guid.NewGuid().ToString();
            Session["GitHub_" + state] = "Unicorn rainbows";

            // Construct the authorization URL
            string authorizatioUrl = client.GetAuthorizationUrl(state);

            // Redirect the user to the OAuth dialog
            return Redirect(authorizatioUrl);

        }

        [HttpGet]
        public ActionResult LinkGitHub(string state, string code = null)
        {

            IPublishedContent profilePage = Umbraco.TypedContent(1057);
            if (profilePage == null) return GetErrorResult("Oh noes! This really shouldn't happen.");

            // Initialize the OAuth client
            GitHubOAuthClient client = new GitHubOAuthClient
            {
                ClientId = WebConfigurationManager.AppSettings["GitHubClientId"],
                ClientSecret = WebConfigurationManager.AppSettings["GitHubClientSecret"]
            };

            // Validate state - Step 1
            if (String.IsNullOrWhiteSpace(state))
            {
                LogHelper.Info<ProfileController>("No OAuth state specified in the query string.");
                return GetErrorResult("No state specified in the query string.");
            }

            // Validate state - Step 2
            string session = Session["GitHub_" + state] as string;
            if (String.IsNullOrWhiteSpace(session))
            {
                LogHelper.Info<ProfileController>("Failed finding OAuth session item. Most likely the session expired.");
                return GetErrorResult("Session expired?");
            }

            // Remove the state from the session
            Session.Remove("GitHub_" + state);

            // Exchange the auth code for an access token
            GitHubTokenResponse accessTokenResponse;
            try
            {
                accessTokenResponse = client.GetAccessTokenFromAuthorizationCode(code);
            }
            catch (Exception ex)
            {
                LogHelper.Error<ProfileController>("Unable to retrieve access token from GitHub API", ex);
                return GetErrorResult("Oh noes! An error happened.");
            }

            // Initialize a new service instance from the retrieved access token
            var service = Skybrud.Social.GitHub.GitHubService.CreateFromAccessToken(accessTokenResponse.Body.AccessToken);

            // Get some information about the authenticated GitHub user
            GitHubGetUserResponse userResponse;
            try
            {
                userResponse = service.User.GetUser();
            }
            catch (Exception ex)
            {
                LogHelper.Error<ProfileController>("Unable to get user information from the GitHub API", ex);
                return GetErrorResult("Oh noes! An error happened.");
            }

            // Get the GitHub username from the API response
            string githubUsername = userResponse.Body.Login;

            // Get the member of the current ID (for comparision and lookup)
            int memberId = Members.GetCurrentMemberId();

            // Get a reference to the member searcher
            BaseSearchProvider searcher = ExamineManager.Instance.SearchProviderCollection[Constants.Examine.InternalMemberSearcher];

            // Initialize new search criteria for the GitHub username
            ISearchCriteria criteria = searcher.CreateSearchCriteria();
            criteria = criteria.RawQuery($"github:{githubUsername}");

            // Check if there are other members with the same GitHub username
            foreach (var result in searcher.Search(criteria))
            {
                if (result.Id != memberId)
                {
                    LogHelper.Info<ProfileController>("Failed setting GitHub username for user with ID " + memberId + ". Username is already used by member with ID " + result.Id + ".");
                    return GetErrorResult("Another member already exists with the same GitHub username.");
                }
            }

            // Get the member from the member service
            var ms = ApplicationContext.Services.MemberService;
            var mem = ms.GetById(memberId);

            // Update the "github" property and save the value
            mem.SetValue("github", userResponse.Body.Login);
            ms.Save(mem);

            // Clear the runtime cache for the member
            ApplicationContext.ApplicationCache.RuntimeCache.ClearCacheItem("MemberData" + mem.Username);

            // Redirect the member back to the profile page
            return RedirectToUmbracoPage(1057);

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnlinkGitHub()
        {

            var ms = Services.MemberService;
            var mem = ms.GetById(Members.GetCurrentMemberId());
            mem.SetValue("github", "");
            ms.Save(mem);

            var memberPreviousUserName = mem.Username;

            ApplicationContext.ApplicationCache.RuntimeCache.ClearCacheItem("MemberData" + memberPreviousUserName);

            return RedirectToCurrentUmbracoPage();

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HandleSubmit(ProfileModel model)
        {
            if (!ModelState.IsValid)
                return CurrentUmbracoPage();

            var ms = Services.MemberService;
            var mem = ms.GetById(Members.GetCurrentMemberId());
            
            if (mem.Email != model.Email && ms.GetByEmail(model.Email) != null)
            {
                ModelState.AddModelError("Email", "A Member with that email already exists");
                return CurrentUmbracoPage();
            }

            var memberPreviousUserName = mem.Username;

            if (model.Password != model.RepeatPassword)
            {
                ModelState.AddModelError("Password", "Passwords need to match");
                ModelState.AddModelError("RepeatPassword", "Passwords need to match");
                return CurrentUmbracoPage();
            }
            
            mem.Name = model.Name ;
            mem.Email = model.Email;
            mem.Username = model.Email;
            mem.SetValue("profileText",model.Bio);
            mem.SetValue("location",model.Location);
            mem.SetValue("company",model.Company);
            mem.SetValue("twitter",model.TwitterAlias);
            
            // Assume it's valid lat/lon data posted - as its a hidden field that a Google Map will update the lat & lon of hidden fields when marker moved
            mem.SetValue("latitude", model.Latitude); 
            mem.SetValue("longitude", model.Longitude);
            
            var avatarService = new AvatarService();
            var avatarImage = avatarService.GetMemberAvatarImage(HostingEnvironment.MapPath($"~{model.Avatar}"));
            if (avatarImage != null && (avatarImage.Width < 400 || avatarImage.Height < 400))
            {
                // Save the rest of the data, but not the new avatar yet as it's too small
                ms.Save(mem);
                ModelState.AddModelError("Avatar", "Please upload an avatar that is at least 400x400 pixels");
                return CurrentUmbracoPage();
            }

            mem.SetValue("avatar", model.Avatar);
            ms.Save(mem);

            if (!string.IsNullOrEmpty(model.Password) && !string.IsNullOrEmpty(model.RepeatPassword) && model.Password == model.RepeatPassword)
                ms.SavePassword(mem, model.Password);

            ApplicationContext.ApplicationCache.RuntimeCache.ClearCacheItem("MemberData" + memberPreviousUserName);
            TempData["success"] = true;

            return RedirectToCurrentUmbracoPage();
        }

        private ActionResult GetErrorResult(string message)
        {

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<style> body { margin: 10px; font-family: sans-serif; } </style>");
            sb.AppendLine(message);
            sb.AppendLine("<p><a href=\"/member/profile/\">Return to your profile</a></p>");


            ContentResult result = new ContentResult();
            result.ContentType = "text/html";
            result.Content = sb.ToString();
            return result;

        }

    }
}
