﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using NuGet;
using Octopus.Deploy.Startup;

namespace Octopus.Deploy.PackageDownloader
{
    class Program
    {
        readonly static PackageDownloader packageDownloader = new PackageDownloader();

        static int Main(string[] args)
        {
            string packageId = null;
            string packageVersion = null;
            bool forcePackageDownload = false;
            string feedId = null;
            string feedUri = null;
            string feedUsername = null;
            string feedPassword = null;
            
            try
            {
                var options = new OptionSet();
                options.Add("packageId=", "Package ID to download", v => packageId = v);
                options.Add("packageVersion=", "Package version to download", v => packageVersion = v);
                options.Add("feedId=", "Id of the NuGet feed", v => feedId = v);
                options.Add("feedUri=", "URL to NuGet feed", v => feedUri = v);
                options.Add("feedUsername=", "[Optional] Username to use for an authenticated NuGet feed", v => feedUsername = v);
                options.Add("feedPassword=", "[Optional] Password to use for an authenticated NuGet feed", v => feedPassword = v);
                options.Add("forcePackageDownload", "[Optional, Flag] if specified, the package will be downloaded even if it is already in the package cache", v => forcePackageDownload = true);

                options.Parse(args);

                SemanticVersion version;
                Uri uri;
                CheckArguments(packageId, packageVersion, feedId, feedUri, feedUsername, feedPassword, out version, out uri);

                SetFeedCredentials(feedUsername, feedPassword, uri);

                string downloadedTo = null;
                string hash = null;
                long size = 0;
                packageDownloader.DownloadPackage(
                    packageId, 
                    version, 
                    feedId,
                    uri, 
                    forcePackageDownload, 
                    out downloadedTo, 
                    out hash, 
                    out size);

                OctopusLogger.VerboseFormat("Package {0} {1} successfully downloaded from feed: '{2}'", packageId, version,
                    feedUri);

                OctopusLogger.SetOctopusVariable("Package.Hash", hash);
                OctopusLogger.SetOctopusVariable("Package.Size", size);
                OctopusLogger.SetOctopusVariable("Package.InstallationDirectoryPath", downloadedTo);
            }
            catch (Exception ex)
            {
                OctopusLogger.ErrorFormat("Failed to download package {0} {1} from feed: '{2}'", packageId, packageVersion,
                    feedUri);
                return ConsoleFormatter.PrintError(ex);
            }

            return 0;
        }

        static void SetFeedCredentials(string feedUsername, string feedPassword, Uri uri)
        {
            var credentials = GetFeedCredentials(feedUsername, feedPassword);
            FeedCredentialsProvider.Instance.SetCredentials(uri, credentials);
            HttpClient.DefaultCredentialProvider = FeedCredentialsProvider.Instance;
        }

        static ICredentials GetFeedCredentials(string feedUsername, string feedPassword)
        {
            ICredentials credentials = CredentialCache.DefaultNetworkCredentials;
            if (!String.IsNullOrWhiteSpace(feedUsername))
            {
                credentials = new NetworkCredential(feedUsername, feedPassword);
            }
            return credentials;
        }

        // ReSharper disable UnusedParameter.Local
        static void CheckArguments(string packageId, string packageVersion, string feedId, string feedUri, string feedUsername, string feedPassword, out SemanticVersion version, out Uri uri)
        {
            if (String.IsNullOrWhiteSpace(packageId))
            {
                throw new ArgumentException("No package ID was specified");
            }

            if (String.IsNullOrWhiteSpace(packageVersion))
            {
                throw new ArgumentException("No package version was specified");
            }

            if (!SemanticVersion.TryParse(packageVersion, out version))
            {
                throw new ArgumentException("Package version specified is not a valid semantic version");
            }

            if (String.IsNullOrWhiteSpace(feedId))
            {
                throw new ArgumentException("No feed ID was specified.");
            }

            if (String.IsNullOrWhiteSpace(feedUri))
            {
                throw new ArgumentException("No feed URI was specified");
            }

            if (!Uri.TryCreate(feedUri, UriKind.RelativeOrAbsolute, out uri))
            {
                throw new ArgumentException("URI specified is not a valid URI");
            }

            if (!String.IsNullOrWhiteSpace(feedUsername) && String.IsNullOrWhiteSpace(feedPassword))
            {
                throw new ArgumentException("A username was specified but no password was provided");
            }
        }
        // ReSharper restore UnusedParameter.Local
    }
}