﻿using System;
using Atlassian.Jira.Remote;
using Newtonsoft.Json.Linq;

namespace Atlassian.Jira
{
    public class CustomField : JiraNamedEntity
    {
        private readonly RemoteField _remoteField;

        /// <summary>
        /// Creates an instance of a CustomField from a remote field definition.
        /// </summary>
        public CustomField(RemoteField remoteField)
            : base(remoteField)
        {
            _remoteField = remoteField;

            if (String.IsNullOrEmpty(this.Id) && !String.IsNullOrEmpty(CustomIdentifier))
            {
                this.Id = $"customfield_{CustomIdentifier}";
            }
        }

        internal RemoteField RemoteField => this._remoteField;

        public string CustomType => _remoteField.Schema?.Custom;

        public string CustomIdentifier => _remoteField.Schema?.CustomId;
    }
}