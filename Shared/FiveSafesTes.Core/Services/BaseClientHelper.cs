using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Net;
using System.Net.Http;

namespace FiveSafesTes.Core.Services
{
  
    public class BaseClientHelper : IBaseClientHelper
    {
        
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        protected readonly string _address;
        protected readonly JsonSerializerOptions _jsonSerializerOptions;
        public KeycloakTokenHelper? _keycloakTokenHelper { get; set; }
        public string _requiredRole { get; set; }
        public string _password { get; set; }
        public string _username { get; set; }

        public bool IgnoreSSL { get; set; }
        

        public BaseClientHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, string address, bool ignoreSSL)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _address = address;

            IgnoreSSL = ignoreSSL;
            
            _jsonSerializerOptions = new JsonSerializerOptions()
            {
                
                PropertyNameCaseInsensitive = true,
            };
            
        }

        public async Task<string> GetTokenForUser(string username, string password, string requiredRole)
        {
            return (await _keycloakTokenHelper.GetTokenForUser(username, password, requiredRole)).token;
        }


        private async Task<T> CallAPIWithReturnType<T>(
            string endPoint,
            StringContent? jsonString = null,
            Dictionary<string, string>? paramlist = null,
            bool usePut = false,
            string? fileParameterName = null,
            IFormFile? file = null,
            HttpMethod httpMethod = null) where T : class?, new()
        {
            if (httpMethod == null)
            {
                Log.Debug("CallAPIWithReturnType httpMethod > is null");
            }

            HttpResponseMessage response = null;
            if (httpMethod != null)
            {
                Log.Debug("CallAPIWithReturnType httpMethod > " + httpMethod);
                response = await ClientHelperRequestAsync(_address + endPoint, httpMethod, jsonString, paramlist, fileParameterName, file);
            }
            else if(jsonString == null && file == null)
            {
                response = await ClientHelperRequestAsync(_address + endPoint, HttpMethod.Get, jsonString, paramlist, fileParameterName, file);
            }
            else if (usePut)
            {
                response = await ClientHelperRequestAsync(_address + endPoint, HttpMethod.Put, jsonString, paramlist, fileParameterName, file);
            }
            else
            {
                response = await ClientHelperRequestAsync(_address + endPoint, HttpMethod.Post, jsonString, paramlist, fileParameterName, file);
            }

            if (response.IsSuccessStatusCode)
            {
                var result = response.Content;
                try
                {
                    var data = await result.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(data) ?? throw new InvalidOperationException();
                }
                catch (Exception e)
                {
                    Log.Error(e, "{Function} Failed deserialising string ", "CallAPIWithReturnType");
                    throw;
                }

            }
            else
            {
                Log.Error("{Function} Invalid Return Code", "CallAPIWithReturnType");
                throw new Exception("API Failure");
            }

            
        }




        protected async Task<HttpResponseMessage> ClientHelperRequestAsync(string endPoint, HttpMethod method, StringContent? jsonString, Dictionary<string, string>? paramlist, string? fileParameterName, IFormFile? fileInfo)
        {
            try
            {
                //Log.Information("{Function} Calling {Address}", "ClientHelperRequestAsync", endPoint);
                var usetoken = true;
                if (string.IsNullOrEmpty(endPoint)) return new HttpResponseMessage { StatusCode = System.Net.HttpStatusCode.BadRequest };
                
                
                HttpClient? apiClient;
                
                apiClient = await CreateClientWithKeycloak();
                
                    
                endPoint = ConstructEndPoint(endPoint, paramlist);

                HttpResponseMessage res = new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
                if (fileInfo != null && fileParameterName != null)
                {
                    Log.Debug("UploadFileAsync httpMethod > fileInfo != null && fileParameterName != null");
                    res = await UploadFileAsync(fileInfo, apiClient, fileParameterName, endPoint);
                }
                else
                {
                    Log.Debug("ClientHelperRequestAsync httpMethod > " + method);
                    if (method == HttpMethod.Get) res = await apiClient.GetAsync(endPoint);
                    if (method == HttpMethod.Post) res = await apiClient.PostAsync(endPoint, jsonString);
                    if (method == HttpMethod.Put) res = await apiClient.PutAsync(endPoint, jsonString);
                    if (method == HttpMethod.Delete) res = await apiClient.DeleteAsync(endPoint);
                }

                if (!res.IsSuccessStatusCode)
                {
                    var stream =res.Content.ReadAsStream();
                    string content = "";
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        // Read the stream content into a string
                        content = reader.ReadToEnd();

                        // Output the string contentsn
                        
                    }

                     Log.Information("{Function} Api returned an error for {endPoint} Response {Res} Error content {Content}", "ClientHelperRequestAsync",endPoint ,  res, content);
                    throw new Exception("API Call Failure: " + res.StatusCode + ": " + res.ReasonPhrase + " " + content);
                }
                Log.Information("{Function} The response {res}", "ClientHelperRequestAsync", res);
                return res;
            }
            catch (Exception ex) {
                Log.Error(ex, "{Function} Crash with Endpoint {Enpoint}", "ClientHelperRequestAsync", endPoint);
                throw;
            }
        }

        private static string ConstructEndPoint(string endPoint, Dictionary<string, string>? paramlist)
        {
            if (paramlist != null)
            {
                if (endPoint.EndsWith("/"))
                {
                    endPoint = endPoint.Substring(0, endPoint.Length - 1);
                }

                if (!endPoint.EndsWith("?"))
                {
                    endPoint += "?";
                }

                var firstparam = true;
                foreach (var item in paramlist)
                {
                    if (firstparam)
                    {
                        firstparam = false;
                    }
                    else
                    {
                        endPoint += "&";
                    }

                    endPoint += item.Key + "=" + item.Value;
                }
            }

            return endPoint;
        }

        protected async Task<HttpClient> CreateClientWithKeycloak()
        {
            var accessToken = "";
            if (_keycloakTokenHelper != null)
            {
                //Log.Information("{Function} First step. Creds are there? {Creds} with username {Username}, Password {Password} and role {Role}", "DareClienCreateClientWithKeycloaktWithoutTokenHelper", _username, _password, _requiredRole);
                accessToken = (await _keycloakTokenHelper.GetTokenForUser(_username, _password, _requiredRole)).token;
            }
            else
            {
                //Log.Information("{Function} Should not be here. Creds are there? {Creds} with username {Username}, Password {Password} and role {Role}", "DareClienCreateClientWithKeycloaktWithoutTokenHelper", _username, _password, _requiredRole);
                if (_httpContextAccessor.HttpContext == null)
                {
                    accessToken = "";
                }
                else
                {
                    accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
                }
            }
            //Log.Information("{Function} NExt step. Creds are there? {Creds} with username {Username}, Password {Password} and role {Role} now has token  {Bearer}", "DareClienCreateClientWithKeycloaktWithoutTokenHelper", _username, _password, _requiredRole, accessToken);
            if (IgnoreSSL)
            {
                Log.Information("{Function} Using No SSL client for {Address}", "CreateClientWithKeycloak", _address);
            }
            var apiClient = IgnoreSSL
                ? _httpClientFactory.CreateClient("nossl")
                : _httpClientFactory.CreateClient();

            if (!string.IsNullOrWhiteSpace(accessToken))
            {


                apiClient.SetBearerToken(accessToken);
            }

            apiClient.DefaultRequestHeaders.Add("Accept", "application/json");
            return apiClient;
        }



        #region Helpers

        protected StringContent GetStringContent<T>(T datasetObj) where T : class?
        {
            
            var jsonString = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(datasetObj, _jsonSerializerOptions),
                Encoding.UTF8,
                "application/json");
            return jsonString;
        }

        public async Task<TOutput?> CallAPIToSendFile<TOutput>(string endPoint, string fileParamaterName, IFormFile file, Dictionary<string, string>? paramList = null) where TOutput : class?, new()
        {
            Log.Debug("CallAPIToSendFile uesing HttpMethod.Post");
            return await CallAPIWithReturnType<TOutput>(endPoint, null, paramList, false, fileParamaterName, file, httpMethod: HttpMethod.Post);
        }
        public async Task<HttpResponseMessage> CallAPI(string endPoint, StringContent? jsonString, Dictionary<string, string>? paramList = null, bool usePut = false)
        {
            return  await ClientHelperRequestAsync(_address + endPoint, HttpMethod.Post, jsonString, paramList, null, null);
        }

        public async Task<byte[]> CallAPIToGetFile(string endPoint, Dictionary<string, string>? paramList = null)
        {
            var response = await ClientHelperRequestAsync(_address + endPoint, HttpMethod.Get, null, paramList,null, null);
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<TOutput?> CallAPI<TInput, TOutput>(string endPoint,TInput model, Dictionary<string, string>? paramList = null, bool usePut = false) where TInput : class? where TOutput : class?, new()
        {
            StringContent? modelString = null;
            if (model != null)
            {
                modelString = GetStringContent<TInput>(model);
            }
            
            return await CallAPIWithReturnType<TOutput>(endPoint, modelString, paramList, usePut);
        }


        public async Task<TOutput?> CallAPIWithoutModel<TOutput>(string endPoint, Dictionary<string, string>? paramList = null, HttpMethod httpMethod = null) where TOutput : class?, new()
        {
            return await CallAPIWithReturnType<TOutput>(endPoint, null, paramList, httpMethod : httpMethod);
        }

        public async Task<TOutput?> CallAPIDelete<TOutput>(string endPoint, Dictionary<string, string>? paramList = null) where TOutput : class?, new()
        {
            return await CallAPIWithReturnType<TOutput>(endPoint, null, paramList, false, null, null, HttpMethod.Delete);
        }

        public async Task<HttpResponseMessage> UploadFileAsync(IFormFile file, HttpClient apiClient, string fileParameterName, string endPoint)
        {
            using (var formData = new MultipartFormDataContent())
            {
                // Attach the IFormFile to the request
                formData.Add(new StreamContent(file.OpenReadStream()), fileParameterName, file.FileName);

                // Send the POST request to the API
                Log.Debug("UploadFileAsync apiClient.PostAsync(endPoint, formData)");
                HttpResponseMessage response = await apiClient.PostAsync(endPoint, formData);
                return response;

            }


        }

        #endregion
    }
}
