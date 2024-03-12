// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;

namespace Microsoft.PowerShell.PSResourceGet.UtilClasses
{

	public sealed class ContainerRegistryInfo
	{
		#region Properties

		public string Name { get; }
		public string Metadata { get; }
		public ResourceType ResourceType { get; }

		#endregion


		#region Constructors

		internal ContainerRegistryInfo(string name, string metadata, string resourceType)

		{
			Name = name ?? string.Empty;
			Metadata = metadata ?? string.Empty;
			ResourceType = string.IsNullOrWhiteSpace(resourceType) ? ResourceType.None :
					(ResourceType)Enum.Parse(typeof(ResourceType), resourceType, ignoreCase: true);
		}

        #endregion

        #region Methods

        internal Hashtable ToHashtable()
		{
			Hashtable hashtable = new Hashtable
			{
				{ "Name", Name },
				{ "Metadata", Metadata },
				{ "ResourceType", ResourceType }
			};

            return hashtable;
		}

        #endregion
    }
}
