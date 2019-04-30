﻿using System.Threading.Tasks;
using Microsoft.Azure.Management.ApiManagement.ArmTemplates.Common;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Microsoft.Azure.Management.ApiManagement.ArmTemplates.Extract
{
    public class AuthorizationServerExtractor: EntityExtractor
    {
        public async Task<string> GetAuthorizationServers(string ApiManagementName, string ResourceGroupName)
        {
            (string azToken, string azSubId) = await auth.GetAccessToken();

            string requestUrl = string.Format("{0}/subscriptions/{1}/resourceGroups/{2}/providers/Microsoft.ApiManagement/service/{3}/authorizationServers?api-version={4}",
               baseUrl, azSubId, ResourceGroupName, ApiManagementName, GlobalConstants.APIVersion);

            return await CallApiManagement(azToken, requestUrl);
        }

        public async Task<string> GetAuthorizationServer(string ApiManagementName, string ResourceGroupName, string authorizationServerName)
        {
            (string azToken, string azSubId) = await auth.GetAccessToken();

            string requestUrl = string.Format("{0}/subscriptions/{1}/resourceGroups/{2}/providers/Microsoft.ApiManagement/service/{3}/authorizationServers/{4}?api-version={5}",
               baseUrl, azSubId, ResourceGroupName, ApiManagementName, authorizationServerName, GlobalConstants.APIVersion);

            return await CallApiManagement(azToken, requestUrl);
        }

        public async Task<Template> GenerateAuthorizationServersARMTemplate(string apimname, string resourceGroup, string singleApiName, List<TemplateResource> apiTemplateResources)
        {
            Console.WriteLine("------------------------------------------");
            Console.WriteLine("Getting authorization servers from service");
            Template armTemplate = GenerateEmptyTemplateWithParameters();

            List<TemplateResource> templateResources = new List<TemplateResource>();

            // isolate api resources in the case of a single api extraction, as they may reference authorization servers
            var apiResources = apiTemplateResources.Where(resource => resource.type == ResourceTypeConstants.API);

            string authorizationServers = await GetAuthorizationServers(apimname, resourceGroup);
            JObject oAuthorizationServers = JObject.Parse(authorizationServers);

            foreach (var item in oAuthorizationServers["value"])
            {
                string authorizationServerName = ((JValue)item["name"]).Value.ToString();
                string authorizationServer = await GetAuthorizationServer(apimname, resourceGroup, authorizationServerName);

                AuthorizationServerTemplateResource authorizationServerTemplateResource = JsonConvert.DeserializeObject<AuthorizationServerTemplateResource>(authorizationServer);
                authorizationServerTemplateResource.name = $"[concat(parameters('ApimServiceName'), '/{authorizationServerName}')]";
                authorizationServerTemplateResource.apiVersion = GlobalConstants.APIVersion;

                // only extract the authorization server if this is a full extraction, or in the case of a single api, if it is referenced by one of the api's authentication settings
                bool isReferencedByAPI = false;
                foreach (APITemplateResource apiResource in apiResources)
                {
                    if (apiResource.properties.authenticationSettings != null &&
                        apiResource.properties.authenticationSettings.oAuth2 != null &&
                        apiResource.properties.authenticationSettings.oAuth2.authorizationServerId != null &&
                        apiResource.properties.authenticationSettings.oAuth2.authorizationServerId.Contains(authorizationServerName))
                    {
                        isReferencedByAPI = true;
                    }
                }
                if (singleApiName == null || isReferencedByAPI)
                {
                    Console.WriteLine("'{0}' Authorization Server found", authorizationServerName);
                    templateResources.Add(authorizationServerTemplateResource);
                }
            }

            armTemplate.resources = templateResources.ToArray();
            return armTemplate;
        }
    }
}
