﻿using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployAzureWebFixture : CalamariFixture
    {
        ICalamariFileSystem fileSystem;
        VariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            const string azureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
            const string webAppName = "octodemo003-dev";
            const string webSpaceName = "southeastasiawebspace";
            const string certificateThumbprint = "86B5C8E5553981FED961769B2DA3028C619596AC";

            // To avoid putting the certificate details in GitHub, we will assume it is stored in the CertificateStore 
            // of the local machine, and ignore the test if not.
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

            if (certificates.Count == 0)
                Assert.Ignore("Azure tests can only run if the expected certificate is present in the Certificate Store");

            variables = new VariableDictionary();
            variables.Set(SpecialVariables.Machine.Azure.CertificateBytes, Convert.ToBase64String(certificates[0].Export(X509ContentType.Pfx)));
            variables.Set(SpecialVariables.Machine.Azure.SubscriptionId, azureSubscriptionId);
            variables.Set(SpecialVariables.Machine.Azure.WebAppName, webAppName);
            variables.Set(SpecialVariables.Machine.Azure.WebSpaceName, webSpaceName);

            fileSystem = new WindowsPhysicalFileSystem();
        }

        [Test]
        public void ShouldDeployPackage()
        {
            var result = DeployPackage("Acme.Web");

            result.AssertZero();

            // Should remove staging directory
            Assert.False(fileSystem.DirectoryExists(result.CapturedOutput.OutputVariables[SpecialVariables.Package.Output.InstallationDirectoryPath]),
                "Staging directory should be deleted");
        }

        CalamariResult DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageName, "1.0.0")))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("deploy-azure-web")
                    .Argument("package", acmeWeb.FilePath)
                    .Argument("variables", variablesFile.FilePath));       
            }
        }
    }
}