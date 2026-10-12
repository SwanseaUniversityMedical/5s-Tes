using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net.Http.Headers;
using FiveSafesTes.Core.Models.Settings;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services
{
    public class KeycloakMinioUserService : IKeycloakMinioUserService
    {
        public  SubmissionKeyCloakSettings _submissionKeyCloakSettings;
        public KeycloakMinioUserService(SubmissionKeyCloakSettings submissionKeyCloakSettings)
        {
            _submissionKeyCloakSettings = submissionKeyCloakSettings;
        }
        public async Task<bool> SetMinioUserAttribute(string accessToken, string userName, string attributeName, string attributeValueToAdd)
        {
            try
            {
                var baseUrl = _submissionKeyCloakSettings.Server;
                var realm = _submissionKeyCloakSettings.Realm;
                var protocol = _submissionKeyCloakSettings.Protocol;
                var attributeKey = "policy";
                var userId = await GetUserIDAsync(accessToken, userName);
                var userAttributesJson = await GetUserAttributesAsync(baseUrl, realm, accessToken, userId, protocol);

                if (userAttributesJson != null)
                {

                    JObject user = JObject.Parse(userAttributesJson);

                    if (user["attributes"] == null)
                    {
                        JObject attributes = new JObject();

                        // Add the "attributes" object to the user object
                        user["attributes"] = attributes;
                    }
                    if (user["attributes"][attributeKey] != null)
                    {
                        var existingValues = user["attributes"][attributeKey].ToObject<JArray>();
                        existingValues.Add(attributeValueToAdd);
                        user["attributes"][attributeKey] = existingValues;
                    }
                    else
                    {
                        user["attributes"][attributeKey] = new JArray(attributeValueToAdd);
                    }


                    string updatedUserData = user.ToString();


                    bool updateResult = await UpdateUserAttributes(baseUrl, realm, userId, accessToken, updatedUserData, protocol);

                    if (updateResult)
                    {
                        Log.Information("{Function} attributes added successfully", "SetMinioUserAttribute");
                        return true;
                    }
                    else
                    {
                        Log.Error("{Function} Failed to update user attributes.", "SetMinioUserAttribute");
                        return true;
                    }
                }
                else
                {
                    Log.Error("{Function} Failed to retrieve user attributes.", "SetMinioUserAttribute");
                    return true;
                }


            }
            catch (Exception ex)
            {

                throw;
            }
        }
        public async Task<bool> RemoveMinioUserAttribute(string accessToken, string userName, string attributeName, string attributeValueToRemove)
        {
          try
          {
            var baseUrl = _submissionKeyCloakSettings.Server;
            var realm = _submissionKeyCloakSettings.Realm;
            var protocol = _submissionKeyCloakSettings.Protocol;
            var userId = await GetUserIDAsync(accessToken, userName);

            if (string.IsNullOrWhiteSpace(userId))
            {
              Log.Error(
                "{Function}: Could not find user {UserName}.",
                nameof(RemoveMinioUserAttribute),
                userName);

              return false;
            }

            var userJson = await GetUserAttributesAsync(
              baseUrl, realm, accessToken, userId, protocol);

            if (string.IsNullOrWhiteSpace(userJson))
            {
              Log.Error(
                "{Function}: Failed to retrieve user {UserName}.",
                nameof(RemoveMinioUserAttribute),
                userName);

              return false;
            }

            var user = JObject.Parse(userJson);
            var attributes = user["attributes"] as JObject;
            if (attributes == null)
            {
              return true;
            }
            var existingValues = attributes[attributeName] as JArray;

            if (existingValues == null)
            {
              return true;
            }

            var valuesToRemove = existingValues
              .Where(value => string.Equals(
                value?.ToString(),
                attributeValueToRemove,
                StringComparison.Ordinal))
              .ToList();

            foreach (var value in valuesToRemove)
            {
              value.Remove();
            }

            if (existingValues.Count == 0)
            {
              attributes.Remove(attributeName);
            }

            var updateResult = await UpdateUserAttributes(baseUrl, realm, userId, accessToken, user.ToString(), protocol);

            if (!updateResult)
            {
              Log.Error(
                "{Function}: Failed to update attribute {AttributeName} for user {UserName}.",
                nameof(RemoveMinioUserAttribute),
                attributeName,
                userName);

              return false;
            }

            return true;
          }
          catch (Exception ex)
          {
            Log.Error(
              ex,
              "{Function}: Error removing attribute {AttributeName} from user {UserName}.",
              nameof(RemoveMinioUserAttribute),
              attributeName,
              userName);

            throw;
          }
        }

        public class MinioStuff{
            public string username { get; set; }
            public string id { get; set; }
        }
        public async Task<string> GetUserIDAsync(string accessToken, string userName)
        // TODO: Handle error better here: if user is not found (e.g., user is not in the group "Dare-Control-Admin"), return null or throw a specific exception
        {
            var baseUrl = _submissionKeyCloakSettings.Server;
            var realm = _submissionKeyCloakSettings.Realm;
            var protocol = _submissionKeyCloakSettings.Protocol;
            HttpClient httpClient = new HttpClient(_submissionKeyCloakSettings.getProxyHandler);
            
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            var apiUrl = $"{protocol}://{baseUrl}/admin/realms/{realm}/users?username={userName}";
            Log.Information("{Function} BaseUrl {BaseUrl} and API Url {ApiUrl}", "GetUserIDAsync", baseUrl, apiUrl);
            var response = await httpClient.GetAsync(apiUrl);


            var jsonString = await response.Content.ReadAsStringAsync();
            try
            {
                Log.Information("{Function} JSONString {JSONString}","GetUserIDAsync", jsonString);
                var jsonObject = JsonConvert.DeserializeObject<List<MinioStuff>>(jsonString);
                foreach (var item in jsonObject)
                {
                    if (item.username.ToString().ToLower() == userName.ToLower())
                    {
                        return item.id.ToString();
                    }
                }

                return string.Empty;
                //throw new Exception("User not found");
            }
            catch (Exception ex)
            {

                throw;
            }


            return string.Empty;
        }
        public async Task<string> GetUserAttributesAsync(string baseUrl, string realm, string accessToken, string userID, string protocol)
        {
            using (var httpClient = new HttpClient(_submissionKeyCloakSettings.getProxyHandler))
            {
                httpClient.BaseAddress = new Uri($"{protocol}://{baseUrl}/admin/realms/{realm}/users/{userID}");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync("");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    return null;
                }
            }
        }
        public async Task<bool> UpdateUserAttributes(string keycloakBaseUrl, string realm, string userId, string accessToken, string updatedUserData, string protocol)
        {
            using (var httpClient = new HttpClient(_submissionKeyCloakSettings.getProxyHandler))
            {
                httpClient.BaseAddress = new Uri($"{protocol}://{keycloakBaseUrl}/admin/realms/{realm}/users/{userId}");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var content = new StringContent(updatedUserData, System.Text.Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync("", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                  Log.Error(
                    "Keycloak user attributes update failed. Status: {StatusCode}. Response: {ResponseBody}",
                    (int)response.StatusCode,
                    responseBody);
                }
                return response.IsSuccessStatusCode;
            }
        }
    }
}
