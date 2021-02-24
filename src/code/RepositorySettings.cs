
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using System;
using System.IO;
using System.Collections.Generic;
using System.Management.Automation;
using System.Xml.Linq;
using System.Linq;
using static System.Environment;

namespace Microsoft.PowerShell.PowerShellGet.RepositorySettings
{
    /// <summary>
    /// Repository settings
    /// </summary>

    class RespositorySettings
    {
        /// <summary>
        /// Default file name for a settings file is 'psresourcerepository.config'
        /// Also, the user level setting file at '%APPDATA%\NuGet' always uses this name
        /// </summary>
        public static readonly string DefaultRepositoryFileName = "PSResourceRepository.xml";
        public static readonly string DefaultRepositoryPath = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "PowerShellGet"); //"%APPDATA%/PowerShellGet";  // c:\code\temp\repositorycache
        public static readonly string DefaultFullRepositoryPath = Path.Combine(DefaultRepositoryPath, DefaultRepositoryFileName);

        public RespositorySettings() { }

        /// <summary>
        /// Find a repository XML
        /// Returns:
        /// </summary>
        /// <param name="sectionName"></param>
        public bool FindRepositoryXML()
        {
            // Search in the designated location for the repository XML
            if (File.Exists(DefaultFullRepositoryPath))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Create a new repository XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public void CreateNewRepositoryXML()
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
        /// Add a repository to the XML
        /// Returns: void
        /// </summary>
        /// <param name="sectionName"></param>
        public void Add(string repoName, Uri repoURL, int repoPriority, bool repoTrusted)
        {
            // Check to see if information we're trying to add to the repository is valid
            if (string.IsNullOrEmpty(repoName))
            {
                // throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
                throw new ArgumentException("Repository name cannot be null or empty");
            }
            if (string.IsNullOrEmpty(repoURL.ToString()))
            {
                // throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(sectionName));
                throw new ArgumentException("Repository URL cannot be null or empty");
            }

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

            // Check if what's being added already exists, if it does throw an error
            var node = doc.Descendants("Repository").Where(e => string.Equals(e.Attribute("Name").Value, repoName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

            if (node != null)
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
        }

        /// <summary>
        /// Updates a repository name, URL, priority, or installation policy
        /// Returns:  void
        /// </summary>
        public void Update(string repoName, Uri repoURL, int repoPriority, bool? repoTrusted)
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
        public void Remove(string[] repoNames)
        {

            // Check to see if information we're trying to add to the repository is valid
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

            foreach (var repo in repoNames)
            {
                // Check if what's being added doesn't already exist, throw an error
                var node = doc.Descendants("Repository").Where(e => string.Equals(e.Attribute("Name").Value, repo, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                if (node == null)
                {
                    throw new ArgumentException(String.Format("Unable to find repository '{0}'.  Use Get-PSResourceRepository to see all available repositories.", repo));
                }

                // Remove item from file
                node.Remove();
            }

            // Close the file
            root.Save(DefaultFullRepositoryPath);
        }

        public List<PSObject> Read(string[] repoNames)
        {
            // Can be null, will just retrieve all
            // Call FindRepositoryXML()  [Create will make a new xml if one doesn't already exist]
            if (!FindRepositoryXML())
            {
                throw new ArgumentException("Was not able to successfully find xml. Try running 'Register-PSResourceRepository -PSGallery'");
            }

            // Open file
            XDocument doc = XDocument.Load(DefaultFullRepositoryPath);

            var foundRepos = new List<PSObject>();
            if (repoNames == null || !repoNames.Any() || string.Equals(repoNames[0], "*") || repoNames[0] == null)
            {
                // array is null and we will list all repositories
                // iterate through the doc
                foreach (var repo in doc.Descendants("Repository"))
                {
                    PSObject repoAsPSObject = new PSObject();
                    repoAsPSObject.Members.Add(new PSNoteProperty("Name", repo.Attribute("Name").Value));
                    repoAsPSObject.Members.Add(new PSNoteProperty("Url", repo.Attribute("Url").Value));
                    repoAsPSObject.Members.Add(new PSNoteProperty("Trusted", repo.Attribute("Trusted").Value));
                    repoAsPSObject.Members.Add(new PSNoteProperty("Priority", repo.Attribute("Priority").Value));

                    foundRepos.Add(repoAsPSObject);
                }
            }
            else
            {
                foreach (var repo in repoNames)
                {
                    // Check to see if repository exists
                    // need to fix the case sensitivity
                    var node = doc.Descendants("Repository").Where(e => string.Equals(e.Attribute("Name").Value, repo, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    if (node != null)
                    {
                        PSObject repoAsPSObject = new PSObject();
                        repoAsPSObject.Members.Add(new PSNoteProperty("Name", node.Attribute("Name").Value));
                        repoAsPSObject.Members.Add(new PSNoteProperty("Url", node.Attribute("Url").Value));
                        repoAsPSObject.Members.Add(new PSNoteProperty("Trusted", node.Attribute("Trusted").Value));
                        repoAsPSObject.Members.Add(new PSNoteProperty("Priority", node.Attribute("Priority").Value));

                        foundRepos.Add(repoAsPSObject);
                    }
                }
            }


            // Sort by priority, then by repo name
            // foundRepos.Sort((x, y) => ( Int32.Parse((x.Members.Where(m => m.Name.Equals("Priority"))).FirstOrDefault().Value.ToString()).CompareTo( Int32.Parse((y.Members.Where(m2 => m2.Name.Equals("Priority"))).FirstOrDefault().Value.ToString()) ) ));
            var reposToReturn = foundRepos.OrderBy(x => (Int32.Parse((x.Members.Where(m => m.Name.Equals("Priority"))).FirstOrDefault().Value.ToString())))
                .ThenBy(x => (x.Members.Where(m => m.Name.Equals("Name"))).FirstOrDefault().Value.ToString());

            return reposToReturn.ToList();
        }
    }
}