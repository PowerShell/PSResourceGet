// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using System.Management.Automation;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;
using static System.Environment;
using Dbg = System.Diagnostics.Debug;


namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// The class contains basic information of a repository path settings as well as methods to
    /// perform CRUD operations on the repository store file.
    /// </summary>

    internal static class RepositorySettings
    {
        /// <summary>
        /// File name for a user's repository store file is 'PSResourceRepository.xml'
        /// The repository store file's location is currently only at '%LOCALAPPDATA%\NuGet' for the user account.
        /// </summary>
        public static readonly string RepositoryFileName = "PSResourceRepository.xml";
        public static readonly string RepositoryPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet"); //"%APPDATA%/PowerShellGet";  // c:\code\temp\repositorycache
        public static readonly string FullRepositoryPath = Path.Combine(RepositoryPath, RepositoryFileName);


        /// <summary>
        /// Check if repository store xml file exists, if not then create
        /// </summary>
        public static void CheckRepositoryStore()
        {
            if(!File.Exists(FullRepositoryPath))
            {
                try{
                    if (!Directory.Exists(RepositoryPath))
                    {
                        Directory.CreateDirectory(RepositoryPath);
                    }

                    XDocument newRepoXML = new XDocument(
                            new XElement("configuration")
                    );
                    newRepoXML.Save(FullRepositoryPath);
                }
                catch (Exception e)
                {
                    throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store creation failed with error: {0}.", e.Message));
                }
            }
            // Open file (which should exist now), if cannot/is corrupted then throw error
            try{
                XDocument.Load(FullRepositoryPath);
            }
            catch(Exception e)
            {
                throw new PSInvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Repository store may be corrupted, file reading failed with error: {0}.", e.Message));
            }
        }

        /// <summary>
        /// Add a repository to the store
        /// Returns: PSRepositoryItem containing information about the repository just added to the repository store
        /// </summary>
        /// <param name="sectionName"></param>
        public static PSRepositoryItem Add(string repoName, Uri repoURL, int repoPriority, bool repoTrusted)
        {
            Dbg.Assert(!string.IsNullOrEmpty(repoName), "Repository name cannot be null or empty");
            Dbg.Assert(!string.IsNullOrEmpty(repoURL.ToString()), "Repository URL cannot be null or empty");

            try{
                // Open file
                XDocument doc = XDocument.Load(FullRepositoryPath);

                if(FindExistingRepositoryHelper(doc, repoName) != null)
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

                root.Add(newElement);

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch(Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Adding to repository store failed: {0}", e.Message));
            }

            PSRepositoryItem repoItem = new PSRepositoryItem(repoName, repoURL, repoPriority, repoTrusted);
            return repoItem;
        }

        /// <summary>
        /// Updates a repository name, URL, priority, or installation policy
        /// Returns:  void
        /// </summary>
        public static void Update(string repoName, Uri repoURL, int repoPriority, bool? repoTrusted)
        {
            Dbg.Assert(!string.IsNullOrEmpty(repoName), "Repository name cannot be null or empty");

            try{
                // Open file
                XDocument doc = XDocument.Load(FullRepositoryPath);

                XElement node = FindExistingRepositoryHelper(doc, repoName);
                if(node == null)
                {
                    throw new ArgumentException("Cannot find the repository because it does not exist. Try registering the repository using 'Register-PSResourceRepository'");
                }

                // Else, keep going
                // Get root of XDocument (XElement)
                var root = doc.Root;

                if (repoURL != null)
                {
                    node.Attribute("Url").Value = repoURL.AbsoluteUri;
                }
                if (repoPriority >= 0)
                {
                    node.Attribute("Priority").Value = repoPriority.ToString();
                }
                // false, setting to true
                // true, setting to false
                // false setting to false
                // true setting to true

                if (repoTrusted != null)
                {
                    node.Attribute("Trusted").Value = repoTrusted.ToString();
                }

                // Close the file
                root.Save(FullRepositoryPath);
            }
            catch(Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Updating to repository store failed: {0}", e.Message));
            }
        }

        /// <summary>
        /// Removes a repository from the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static void Remove(string[] repoNames, out string[] errorMsgs)
        {
            errorMsgs = null;
            List<string> temp = new List<string>();

            // Check to see if information we're trying to remove from the repository is valid
            if (repoNames == null || repoNames.Length == 0)
            {
                throw new ArgumentException("Repository name cannot be null or empty");
            }
            XDocument doc;
            try {
                // Open file
                doc = XDocument.Load(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            // Get root of XDocument (XElement)
            var root = doc.Root;

            foreach (string repo in repoNames)
            {
                XElement node = FindExistingRepositoryHelper(doc, repo);
                if(node == null)
                {
                    temp.Add(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    continue;
                }
                // Remove item from file
                node.Remove();
            }
            // Close the file
            root.Save(FullRepositoryPath);
            errorMsgs = temp.ToArray();
        }

        public static List<PSRepositoryItem> Read(string[] repoNames, out string[] errorMsgs)
        {
            errorMsgs = null;
            List<string> temp = new List<string>();
            var foundRepos = new List<PSRepositoryItem>();

            XDocument doc;
            try {
                // Open file
                doc = XDocument.Load(FullRepositoryPath);
            }
            catch (Exception e)
            {
                throw new PSInvalidOperationException(String.Format("Loading repository store failed: {0}", e.Message));
            }

            if(repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // Name array or single value is null so we will list all repositories registered
                // iterate through the doc
                foreach(XElement repo in doc.Descendants("Repository"))
                {
                    if(!Uri.TryCreate(repo.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                    {
                        temp.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", repo.Attribute("Name").Value));
                        continue;
                    }
                    PSRepositoryItem currentRepoItem = new PSRepositoryItem(repo.Attribute("Name").Value,
                        thisUrl,
                        Int32.Parse(repo.Attribute("Priority").Value),
                        Boolean.Parse(repo.Attribute("Trusted").Value));

                    foundRepos.Add(currentRepoItem);
                }
            }
            else
            {
                foreach(string repo in repoNames)
                {
                    bool repoMatch = false;
                    WildcardPattern nameWildCardPattern = new WildcardPattern(repo, WildcardOptions.IgnoreCase);

                    foreach(var node in doc.Descendants("Repository").Where(e => nameWildCardPattern.IsMatch(e.Attribute("Name").Value)))
                    {
                        repoMatch = true;
                        if(!Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out Uri thisUrl))
                        {
                            //debug statement
                            temp.Add(String.Format("Unable to read incorrectly formatted URL for repo {0}", node.Attribute("Name").Value));
                            continue;
                        }
                        PSRepositoryItem currentRepoItem = new PSRepositoryItem(node.Attribute("Name").Value,
                            thisUrl,
                            Int32.Parse(node.Attribute("Priority").Value),
                            Boolean.Parse(node.Attribute("Trusted").Value));

                        foundRepos.Add(currentRepoItem);
                    }
                    if(!repoMatch)
                    {
                        temp.Add(String.Format("Unable to find repository matching Name '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                    }
                }
            }

            errorMsgs = temp.ToArray();
            // Sort by priority, then by repo name
            var reposToReturn = foundRepos.OrderBy(x => x.Priority).ThenBy(x => x.Name);
            return reposToReturn.ToList();
        }

        private static XElement FindExistingRepositoryHelper(XDocument doc, string name)
        {
            XElement node = doc.Descendants("Repository").Where(e => string.Equals(e.Attribute("Name").Value, name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if(node != null)
            {
                return node;
            }
            else {
                return null;
            }
        }
    }
}
