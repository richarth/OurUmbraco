﻿using System;
using System.Collections.Generic;

namespace OurUmbraco.MarketPlace.Interfaces
{
    //TODO: All of this data should probably be in the Lucene index! 
    // Then we can use that index for querying and returning these results. Alternatively, another DB table 
    // should be created to store all of this normalized data.
    public interface IListingItem
    {
        int Id { get; set; }
        string NiceUrl { get; set; }
        string Name { get; set; }
        string Description { get; set; }
        string CurrentVersion { get; set; }
        string CurrentReleaseFile { get; set; }
        string DefaultScreenshot { get; set; }
        string DevelopmentStatus { get; set; }
        IEnumerable<IProjectTag> Tags { get;set; }
        int CategoryId { get; set; }
        string Logo { get; set; }
        String LicenseName { get; set; }
        String LicenseUrl { get; set; }
        ListingType ListingType { get; set; }
        string SupportUrl { get; set; }
        IEnumerable<IMediaFile> PackageFile { get; set; }
        IEnumerable<IMediaFile> HotFixes { get; set; }
        IEnumerable<IMediaFile> SourceFile { get; set; }
        IEnumerable<IMediaFile> DocumentationFile { get; set; }
        IVendor Vendor { get; set; }
        int VendorId { get; set; }
        double Price { get; set; }
        string GACode { get; set; }
        IEnumerable<IMediaFile> ScreenShots { get; set; }
        string DemonstrationUrl { get; set; }
        string[] UmbracoVerionsSupported { get; set; }
        string[] NETVersionsSupported { get; set; }
        TrustLevel TrustLevelSupported { get; set; }
        DateTime TermsAgreementDate { get; set; }
        DateTime CreateDate { get; set; }
        Guid ProjectGuid { get; set; }
        Boolean Stable { get; set; }
        Boolean Approved { get; set; }
        Boolean StarterKitModule { get; set; }
        Boolean NotAPackage { get; set; }
        Boolean OpenForCollab { get; set; }
        Boolean Live { get; set; }
        Boolean Disabled { get; set; }
        String SourceCodeUrl { get; set; }
        int Downloads { get; }
        int Karma { get; }
        string ProjectUrl { get; set; }
        Guid Version { get; }
        String LicenseKey { get; set; }
        int ProjectViews { get; }
    }
}
