﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Salesforce.Common.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Salesforce.Common.Serializer;

namespace Salesforce.Common
{
    public class ServiceHttpClient : IServiceHttpClient, IDisposable
    {
        private const string UserAgent = "forcedotcom-toolkit-dotnet";
        private const string DateFormat = "s";
        private readonly string _instanceUrl;
        public string ApiVersion;
        private readonly HttpClient _httpClient;

        public ServiceHttpClient(string instanceUrl, string apiVersion, string accessToken, HttpClient httpClient)
        {
            _instanceUrl = instanceUrl;
            ApiVersion = apiVersion;
            _httpClient = httpClient;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(string.Concat(UserAgent, "/", ApiVersion));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task<T> HttpGetAsync<T>(string urlSuffix)
        {
            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = HttpMethod.Get
            };

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var jToken = JToken.Parse(response);
                if (jToken.Type == JTokenType.Array)
                {
                    var jArray = JArray.Parse(response);

                    var r = JsonConvert.DeserializeObject<T>(jArray.ToString());
                    return r;
                }
                else
                {
                    var jObject = JObject.Parse(response);

                    var r = JsonConvert.DeserializeObject<T>(jObject.ToString());
                    return r;
                }
            }

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }

        public async Task<T> HttpGetRestApiAsync<T>(string apiName)
        {
            var url = Common.FormatRestApiUrl(apiName, _instanceUrl);

            return await HttpGetAsync<T>(url);
        }

        public async Task<IList<T>> HttpGetAsync<T>(string urlSuffix, string nodeName)
        {
            string next = null;
            string response = null;
            var records = new List<T>();

            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            try
            {
                do
                {
                    if (next != null)
                        uri = Common.FormatUrl(string.Format("query/{0}", next.Split('/').Last()), _instanceUrl, ApiVersion);

                    var request = new HttpRequestMessage
                    {
                        RequestUri = uri,
                        Method = HttpMethod.Get
                    };

                    var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        var jObject = JObject.Parse(response);
                        var jToken = jObject.GetValue(nodeName);

                        next = (jObject.GetValue("nextRecordsUrl") != null) ? jObject.GetValue("nextRecordsUrl").ToString() : null;
                        records.AddRange(JsonConvert.DeserializeObject<IList<T>>(jToken.ToString()));
                    }
                } while (!string.IsNullOrEmpty(next));

                return (IList<T>)records;
            }
            catch (ForceException)
            {
                var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
                throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
            }
        }

        public async Task<T> HttpGetAsync<T>(Uri uri)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = HttpMethod.Get
            };

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var r = JsonConvert.DeserializeObject<T>(response);
                return r;
            }

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }

        public async Task<T> HttpPostRestApiAsync<T>(string apiName, object inputObject)
        {
            var url = Common.FormatRestApiUrl(apiName, _instanceUrl);

            return await HttpPostAsync<T>(inputObject, url);
        }

        public async Task<T> HttpPostAsync<T>(object inputObject, string urlSuffix)
        {
            return await HttpPostAsync<T>(inputObject, urlSuffix, null);
        }


        public async Task<T> HttpPostAsync<T>(object inputObject, string urlSuffix, Dictionary<string, string> headers)
        {
            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = new HttpMethod("POST"),

            };

            if (headers != null)
            {
                foreach (var h in headers)
                {
                    request.Headers.Add(h.Key, h.Value);
                }
            }

            var json = JsonConvert.SerializeObject(inputObject,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CreateableContractResolver(),
                    DateFormatString = DateFormat
                });

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var r = JsonConvert.DeserializeObject<T>(response);
                return r;
            }

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }

        public async Task<T> HttpPostAsync<T>(object inputObject, Uri uri)
        {
            return await HttpPostAsync<T>(inputObject, uri, null);
        }

        public async Task<T> HttpPostAsync<T>(object inputObject, Uri uri, Dictionary<string, string> headers)
        {
            var json = JsonConvert.SerializeObject(inputObject,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = DateFormat
                });

            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = new HttpMethod("POST")
            };

            if (headers != null)
            {
                foreach (var h in headers)
                {
                    request.Headers.Add(h.Key, h.Value);
                }
            }

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var r = JsonConvert.DeserializeObject<T>(response);
                return r;
            }

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }

        public async Task<SuccessResponse> HttpPatchAsync(object inputObject, string urlSuffix)
        {
            return await HttpPatchAsync(inputObject, urlSuffix, null);
        }


        public async Task<SuccessResponse> HttpPatchAsync(object inputObject, string urlSuffix, Dictionary<string, string> headers)
        {
            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = new HttpMethod("PATCH")
            };

            if (headers != null)
            {
                foreach (var h in headers)
                {
                    request.Headers.Add(h.Key, h.Value);
                }
            }

            var json = JsonConvert.SerializeObject(inputObject,
                Formatting.None,
                new JsonSerializerSettings
                {
                    ContractResolver = new UpdateableContractResolver(),
                    DateFormatString = DateFormat
                });

            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                if (responseMessage.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

                    var r = JsonConvert.DeserializeObject<SuccessResponse>(response);
                    return r;
                }

                var success = new SuccessResponse { Id = "", Errors = "", Success = "true" };
                return success;
            }

            var error = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(error);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }
        

        public async Task<bool> HttpDeleteAsync(string urlSuffix)
        {
            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = HttpMethod.Delete
            };

            var responseMessage = await _httpClient.SendAsync(request).ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                return true;
            }

            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }

        public async Task<T> HttpBinaryDataPostAsync<T>(string urlSuffix, object inputObject, byte[] fileContents, string headerName, string fileName)
        {
            var uri = Common.FormatUrl(urlSuffix, _instanceUrl, ApiVersion);

            var json = JsonConvert.SerializeObject(inputObject,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                });

            var content = new MultipartFormDataContent();

            var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            stringContent.Headers.Add("Content-Disposition", "form-data; name=\"json\"");
            content.Add(stringContent);

            var byteArrayContent = new ByteArrayContent(fileContents);
            byteArrayContent.Headers.Add("Content-Type", "application/octet-stream");
            byteArrayContent.Headers.Add("Content-Disposition", String.Format("form-data; name=\"{0}\"; filename=\"{1}\"", headerName, fileName));
            content.Add(byteArrayContent, headerName, fileName);

            var responseMessage = await _httpClient.PostAsync(uri, content).ConfigureAwait(false);
            var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (responseMessage.IsSuccessStatusCode)
            {
                var r = JsonConvert.DeserializeObject<T>(response);
                return r;
            }

            var errorResponse = JsonConvert.DeserializeObject<ErrorResponses>(response);
            throw new ForceException(errorResponse[0].ErrorCode, errorResponse[0].Message);
        }
    }
}
