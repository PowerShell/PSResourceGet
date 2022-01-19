// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// The class contains basic information of a repository path settings as well as methods to
    /// perform Create/Read/Update/Delete operations on the repository store file.
    /// </summary>
    internal static class RepositorySettings
    {
        #region Members

        // File name for a user's repository store file is 'PSResourceRepository.xml'
        // The repository store file's location is currently only at '%LOCALAPPDATA%\PowerShellGet' for the user account.
        private const string PSGalleryRepoName = "PSGallery";
        private const string PSGalleryRepoURL = "https://www.powershellgallery.com/api/v2";
        private const int defaultPriority = 50;
        private const bool defaultTrusted = false;
        private const string RepositoryFileName = "PSResourceRepository.xml";
        private static readonly string RepositoryPath = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "PowerShellGet");
        private static readonly string FullRepositoryPath = Path.Combine(RepositoryPath, RepositoryFileName);

        #endregion

        #region Public methods

        /// <summary>
        /// Check if repository store xml file exists, if not then create
        /// </summary>
        public static void CheckRepositoryStore()
        {
            if (!File.Exists(FullRepositoryPath))
            {
                try
                {
                    if (!Directory.Exists(RepositoryPath))
                    {
                        Directory.CreateDirectory(RepositoryPath);
                    }

                    XDocument newRepoXML = new XDocument(
                        new XElement("configuration"));
                    newRepoXML.Save(FullRepositoryPath);
                }
                catch (Exception e)
                {
                    throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store creation failed with error: {0}.", e.Message));
                }

                // Add PSGallery to the newly created store
                Uri psGalleryUri = new Uri(PSGalleryRepoURL);
                Add(PSGalleryRepoName, psGalleryUri, defaultPriority, defaultTrusted, repoCredentialInfo: null);
            }

            // Open file (which should exist now), if cannot/is corrupted then throw error
            try
            {
                LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store may be corrupted, file reading failed with error: {0}.", e.Message));
            }
        }

        /// <summary>
        /// Add a repository to the store
        /// Returns: PSRepositoryInfo containing information about the repository just added to the repository store
        /// </summary>
        /// <param name="sectionName"></param>
        public static PSRepositoryInfo Add(string repoName, Uri repoURL, int repoPriority, bool repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                if (FindRepositoryElement(doc, repoName) != null)
                {
                    throw new PSInvalidOperationException(String.Format("The PSResource Repository '{0}' already exists.", repoName));
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // Create new element
                XElement newElement = new XElement(
                    "Repository",
                    new XAttribute("Name", repoName),
                    new XAttribute("Url", repoURL),
                    new XAttribute("Priority", repoPriority),
                    new XAttribute("Trusted", repoTrusted)
                    );

                if (repoCredentialInfo != null)
                {
                    newElement.Add(new XAttribute(PSCredentialInfo.VaultNameAttribute, repoCredentialInfo.VaultName));
                    newElement.Add(new XAttribute(PSCredentialInfo.SecretNameAttribute, repoCredentialInfo.SecretName));
                }

                root.Add(newElement);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Adding to repository store failed: {0}", e.Message));
            }

            return new PSRepositoryInfo(repoName, repoURL, repoPriority, repoTrusted, repoCredentialInfo);
        }

        /// <summary>
        /// Updates a repository name, URL, priority, installation policy, or credential information
        /// Returns:  void
        /// </summary>
        public static PSRepositoryInfo Update(string repoName, Uri repoURL, int repoPriority, bool? repoTrusted, PSCredentialInfo repoCredentialInfo)
        {
            PSRepositoryInfo updatedRepo;
            try
            {
                // Open file
                XDocument doc = LoadXDocument(FullRepositoryPath);
                XElement node = FindRepositoryElement(doc, repoName);
                if (node == null)
                {
                    throw new ArgumentException("Cannot find the repository because it does not exist. Try registering the repository using 'Register-PSResourceRepository'");
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                // A null URL value passed in signifies the URL was not attempted to be set.
                // So only set Url attribute if non-null value passed in for repoUrl
                if (repoURL != null)
                {
                    node.Attribute("Url").Value = repoURL.AbsoluteUri;
                }

                // A negative Priority value passed in signifies the Priority value was not attempted to be set.
                // So only set Priority attribute if non-null value passed in for repoPriority
                if (repoPriority >= 0)
                {
                    node.Attribute("Priority").Value = repoPriority.ToString();
                }

                // A null Trusted value passed in signifies the Trusted value was not attempted to be set.
                // So only set Trusted attribute if non-null value passed in for repoTrusted.
                if (repoTrusted != null)
                {
                    node.Attribute("Trusted").Value = repoTrusted.ToString();
                }

                // A null CredentialInfo value passed in signifies that CredentialInfo was not attempted to be set.
                // Set VaultName and SecretName attributes if non-null value passed in for repoCredentialInfo
                if (repoCredentialInfo != null)
                {
                    if (node.Attribute(PSCredentialInfo.VaultNameAttribute) == null)
                    {
                        node.Add(new XAttribute(PSCredentialInfo.VaultNameAttribute, repoCredentialInfo.VaultName));
                    }
                    else
                    {
                        node.Attribute(PSCredentialInfo.VaultNameAttribute).Value = repoCredentialInfo.VaultName;
                    }

                    if (node.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                    {
                        node.Add(new XAttribute(PSCredentialInfo.SecretNameAttribute, repoCredentialInfo.SecretName));
                    }
                    else
                    {
                        node.Attribute(PSCredentialInfo.SecretNameAttribute).Value = repoCredentialInfo.SecretName;
                    }
                }

                // Create Uri from node Url attribute to create PSRepositoryInfo item to return.
                if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                {
                    throw new PSInvalidOperationException(String.Format("Unable to read incorrectly formatted URL for repo {0}", repoName));
                }

                // Create CredentialInfo based on new values or whether it was empty to begin with
                PSCredentialInfo thisCredentialInfo = null;
                if (node.Attribute(PSCredentialInfo.VaultNameAttribute)?.Value != null &&
                    node.Attribute(PSCredentialInfo.SecretNameAttribute)?.Value != null)
                {
                    thisCredentialInfo = new PSCredentialInfo(
                        node.Attribute(PSCredentialInfo.VaultNameAttribute).Value,
                        node.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                }

                updatedRepo = new PSRepositoryInfo(repoName,
                    thisUrl,
                    Int32.Parse(node.Attribute("Priority").Value),
                    Boolean.Parse(node.Attribute("Trusted").Value),
                    thisCredentialInfo);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Updating to repository store failed: {0}", e.Message));
            }

            return updatedRepo;
        }

        /// <summary>
        /// Removes a repository from the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static List<PSRepositoryInfo> Remove(string[] repoNames, out string[] errorList)
        {
            List<PSRepositoryInfo> removedRepos = new List<PSRepositoryInfo>();
            List<string> tempErrorList = new List<string>();
            XDocument doc;
            try
            {
                // Open file
                doc = LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            // Get root of XDocument (XElement)
            var root = doc.Root;

            foreach (string repo in repoNames)
            {
                XElement node = FindRepositoryElement(doc, repo);
                if (node == null)
                {
                    tempErrorList.Add(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    continue;
                }

                PSCredentialInfo repoCredentialInfo = null;
                if (node.Attribute("VaultName") != null & node.Attribute("SecretName") != null)
                {
                    repoCredentialInfo = new PSCredentialInfo(node.Attribute("VaultName").Value, node.Attribute("SecretName").Value);
                }
                removedRepos.Add(
                    new PSRepositoryInfo(repo,
                        new Uri(node.Attribute("Url").Value),
                        Int32.Parse(node.Attribute("Priority").Value),
                        Boolean.Parse(node.Attribute("Trusted").Value),
                        repoCredentialInfo));
                // Remove item from file
                node.Remove();
            }

            // Close the file
            root.Save(FullRepositoryPath);
            errorList = tempErrorList.ToArray();
            
            return removedRepos;
        }

        public static List<PSRepositoryInfo> Read(string[] repoNames, out string[] errorList)
        {
            List<string> tempErrorList = new List<string>();
            var foundRepos = new List<PSRepositoryInfo>();

            XDocument doc;
            try
            {
                // Open file
                doc = LoadXDocument(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            if (repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // Name array or single value is null so we will list all repositories registered
                // iterate through the doc
                foreach (XElement repo in doc.Descendants("Repository"))
                {
                    if (!Uri.TryCreate(repo.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                    {
                        tempErrorList.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", repo.Attribute("Name").Value));
                        continue;
                    }

                    PSCredentialInfo thisCredentialInfo;
                    string credentialInfoErrorMessage = $"Repository {repo.Attribute("Name").Value} has invalid CredentialInfo. {PSCredentialInfo.VaultNameAttribute} and {PSCredentialInfo.SecretNameAttribute} should both be present and non-empty";
                    // both keys are present
                    if (repo.Attribute(PSCredentialInfo.VaultNameAttribute) != null && repo.Attribute(PSCredentialInfo.SecretNameAttribute) != null)
                    {
                        try
                        {
                            // both values are non-empty
                            // = valid credentialInfo
                            thisCredentialInfo = new PSCredentialInfo(repo.Attribute(PSCredentialInfo.VaultNameAttribute).Value, repo.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                        }
                        catch (Exception)
                        {
                            thisCredentialInfo = null;
                            tempErrorList.Add(credentialInfoErrorMessage);
                            continue;
                        }
                    }
                    // both keys are missing
                    else if (repo.Attribute(PSCredentialInfo.VaultNameAttribute) == null && repo.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                    {
                        // = valid credentialInfo
                        thisCredentialInfo = null;
                    }
                    // one of the keys is missing
                    else
                    {
                        thisCredentialInfo = null;
                        tempErrorList.Add(credentialInfoErrorMessage);
                        continue;
                    }

                    PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(repo.Attribute("Name").Value,
                        thisUrl,
                        Int32.Parse(repo.Attribute("Priority").Value),
                        Boolean.Parse(repo.Attribute("Trusted").Value),
                        thisCredentialInfo);

                    foundRepos.Add(currentRepoItem);
                }
            }
            else
            {
                foreach (string repo in repoNames)
                {
                    bool repoMatch = false;
                    WildcardPattern nameWildCardPattern = new WildcardPattern(repo, WildcardOptions.IgnoreCase);

                    foreach (var node in doc.Descendants("Repository").Where(e => nameWildCardPattern.IsMatch(e.Attribute("Name").Value)))
                    {
                        repoMatch = true;
                        if (!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                        {
                            //debug statement
                            tempErrorList.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", node.Attribute("Name").Value));
                            continue;
                        }

                        PSCredentialInfo thisCredentialInfo;
                        string credentialInfoErrorMessage = $"Repository {node.Attribute("Name").Value} has invalid CredentialInfo. {PSCredentialInfo.VaultNameAttribute} and {PSCredentialInfo.SecretNameAttribute} should both be present and non-empty";
                        // both keys are present
                        if (node.Attribute(PSCredentialInfo.VaultNameAttribute) != null && node.Attribute(PSCredentialInfo.SecretNameAttribute) != null)
                        {
                            try
                            {
                                // both values are non-empty
                                // = valid credentialInfo
                                thisCredentialInfo = new PSCredentialInfo(node.Attribute(PSCredentialInfo.VaultNameAttribute).Value, node.Attribute(PSCredentialInfo.SecretNameAttribute).Value);
                            }
                            catch (Exception)
                            {
                                thisCredentialInfo = null;
                                tempErrorList.Add(credentialInfoErrorMessage);
                                continue;
                            }
                        }
                        // both keys are missing
                        else if (node.Attribute(PSCredentialInfo.VaultNameAttribute) == null && node.Attribute(PSCredentialInfo.SecretNameAttribute) == null)
                        {
                            // = valid credentialInfo
                            thisCredentialInfo = null;
                        }
                        // one of the keys is missing
                        else
                        {
                            thisCredentialInfo = null;
                            tempErrorList.Add(credentialInfoErrorMessage);
                            continue;
                        }

                        PSRepositoryInfo currentRepoItem = new PSRepositoryInfo(node.Attribute("Name").Value,
                            thisUrl,
                            Int32.Parse(node.Attribute("Priority").Value),
                            Boolean.Parse(node.Attribute("Trusted").Value),
                            thisCredentialInfo);

                        foundRepos.Add(currentRepoItem);
                    }

                    if (!repo.Contains("*") && !repoMatch)
                    {
                        tempErrorList.Add(String.Format("Unable to find repository with Name '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    }
                }
            }

            errorList = tempErrorList.ToArray();
            // Sort by priority, then by repo name
            var reposToReturn = foundRepos.OrderBy(x => x.Priority).ThenBy(x => x.Name);
            return reposToReturn.ToList();
        }

        #endregion

        #region Private methods

        private static XElement FindRepositoryElement(XDocument doc, string name)
        {
            return doc.Descendants("Repository").Where(
                e => string.Equals(
                    e.Attribute("Name").Value,
                    name,
                    StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
        }

        private static readonly XmlReaderSettings XDocReaderSettings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Prohibit,     // Disallow any DTD elements
            XmlResolver = null,                         // Do not resolve external links
            CheckCharacters = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 1024,
            MaxCharactersInDocument = 512 * 1024 * 1024, // 512M characters = 1GB
            ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None,
            ValidationType = ValidationType.None
        };

        private static XDocument LoadXDocument(string filePath)
        {
            using var xmlReader = XmlReader.Create(filePath, XDocReaderSettings);
            return XDocument.Load(xmlReader);
        }

        #endregion
    }
}
