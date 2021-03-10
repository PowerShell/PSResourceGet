// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using System.Management.Automation;
using System.Xml.Linq;
using System.Linq;
using static System.Environment;
using Microsoft.PowerShell.PowerShellGet.UtilClasses;
using Dbg = System.Diagnostics.Debug;

namespace Microsoft.PowerShell.PowerShellGet.UtilClasses
{
    /// <summary>
    /// The class contains basic information of a repository path settings as well as methods to
    /// perform CRUD operations on the repositories.
    /// </summary>

    internal static class RepositorySettings
    {
        /// <summary>
        /// Default file name for a settings file is 'psresourcerepository.config'
        /// Also, the user level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        public static readonly string DefaultRepositoryFileName = "PSResourceRepository.xml";
        public static readonly string DefaultRepositoryPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet"); //"%APPDATA%/PowerShellGet";  // c:\code\temp\repositorycache
        public static readonly string DefaultFullRepositoryPath = Path.Combine(DefaultRepositoryPath, DefaultRepositoryFileName);

        /// <summary>
        /// Find a repository XML
        /// Returns:
        /// </summary>
        /// <param name="sectionName"></param>
        public static bool FindRepositoryXML()
        {
            // Search in the designated location for the repository XML
            return File.Exists(DefaultFullRepositoryPath);
        }

        /// <summary>
        /// Create a new repository XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static void CreateNewRepositoryXML()
        {
            // Check to see if the file already exists; if it does return
            if (FindRepositoryXML())
            {
                return;
            }

            // create directory if needed
            if (!Directory.Exists(DefaultRepositoryPath))
            {
                Directory.CreateDirectory(DefaultRepositoryPath);
            }

            // If the repository xml file doesn't exist yet, create one
            XDocument newRepoXML = new XDocument(
                    new XElement("configuration")
            );

            // Should be saved in:
            newRepoXML.Save(DefaultFullRepositoryPath);
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


            // Create will make a new XML if one doesn't already exist
            try
            {
                CreateNewRepositoryXML();
            }
            catch
            {
                throw new ArgumentException("Was not able to successfully create xml");
            }

            // Open file
            XDocument doc = XDocument.Load(DefaultFullRepositoryPath);

            // could also call FindExistingRepoHelper()
            if(Read(new []{ repoName }).Count() != 0)
            {
                throw new ArgumentException(String.Format("The PSResource Repository '{0}' already exists.", repoName));
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
            root.Save(DefaultFullRepositoryPath);

            PSRepositoryItem repoItem = new PSRepositoryItem(repoName, repoURL, repoPriority, repoTrusted);

            return repoItem;
        }

        /// <summary>
        /// Updates a repository name, URL, priority, or installation policy
        /// Returns:  void
        /// </summary>
        public static void Update(string repoName, Uri repoURL, int repoPriority, bool? repoTrusted)
        {
            // Check to see if information we're trying to add to the repository is valid
            if (string.IsNullOrEmpty(repoName))
            {
                // throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
                throw new ArgumentException("Repository name cannot be null or empty");
            }

            // We expect the xml to exist, if it doesn't user needs to register a repository
            try
            {
                FindRepositoryXML();
            }
            catch
            {
                throw new ArgumentException("Was not able to successfully find xml. Try running 'Register-PSResourceRepository -PSGallery'");
            }

            // Open file
            XDocument doc = XDocument.Load(DefaultFullRepositoryPath);

            // Check if what's being updated is actually there first
            var node = doc.Descendants("Repository").Where(e => string.Equals(e.Attribute("Name").Value, repoName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (node == null)
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
            root.Save(DefaultFullRepositoryPath);
        }

        /// <summary>
        /// Removes a repository from the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public static void Remove(string[] repoNames)
        {

            // Check to see if information we're trying to remove from the repository is valid
            // repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null
            if (repoNames == null || repoNames.Length == 0)
            {
                // throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
                throw new ArgumentException("Repository name cannot be null or empty");
            }

            if (!FindRepositoryXML())
            {
                throw new ArgumentException("Was not able to successfully find xml. Try running 'Register-PSResourceRepository -PSGallery'");
            }

            // Open file
            XDocument doc = XDocument.Load(DefaultFullRepositoryPath);

            // Get root of XDocument (XElement)
            var root = doc.Root;

            foreach (string repo in repoNames)
            {
                XElement node = FindExistingRepositoryHelper(doc, repo);
                if(node == null)
                {
                    throw new ArgumentException(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                }

                // Remove item from file
                node.Remove();
            }

            // Close the file
            root.Save(DefaultFullRepositoryPath);
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


        public static List<PSRepositoryItem> Read(string[] repoNames)
        {
            // Can be null, will just retrieve all
            // Call FindRepositoryXML()  [Create will make a new xml if one doesn't already exist]
            if (!FindRepositoryXML())
            {
                throw new ArgumentException("Was not able to successfully find xml. Try running 'Register-PSResourceRepository -PSGallery'");
            }

            // Open file
            XDocument doc = XDocument.Load(DefaultFullRepositoryPath);

            var foundRepos = new List<PSRepositoryItem>();
            if(repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // Name array or single value is null so we will list all repositories registered
                // iterate through the doc
                foreach(XElement repo in doc.Descendants("Repository"))
                {
                    Uri thisUrl;
                    // need more error checks for Uri scheme? ideally uri's registered should be already checked
                    Uri.TryCreate(repo.Attribute("Url").Value, UriKind.Absolute, out thisUrl);
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
                    XElement node = FindExistingRepositoryHelper(doc, repo);
                    if(node != null)
                        {
                        Uri thisUrl;
                        // need more error checks for Uri scheme? ideally uri's registered should be already checked
                        Uri.TryCreate(node.Attribute("Url").Value, UriKind.Absolute, out thisUrl);
                        PSRepositoryItem currentRepoItem = new PSRepositoryItem(node.Attribute("Name").Value,
                            thisUrl,
                            Int32.Parse(node.Attribute("Priority").Value),
                            Boolean.Parse(node.Attribute("Trusted").Value));

                        foundRepos.Add(currentRepoItem);
                    }
                }
            }

            // Sort by priority, then by repo name
            var reposToReturn = foundRepos.OrderBy(x => x.Priority).ThenBy(x => x.Name);
            return reposToReturn.ToList();
        }
    }
}
