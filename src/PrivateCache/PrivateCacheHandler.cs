﻿using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ClientSamples.CachingTools;

namespace Tavis.PrivateCache
{
    public class PrivateCacheHandler : DelegatingHandler
    {
        private readonly HttpCache _httpCache;


        public PrivateCacheHandler(HttpMessageHandler innerHandler, HttpCache httpCache)
        {
            _httpCache = httpCache;
            InnerHandler = innerHandler;
        }

        // Process Request and Response
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var queryResult = await _httpCache.QueryCacheAsync(request);

            if (queryResult.Status == CacheStatus.ReturnStored)
            {
                return queryResult.GetHttpResponseMessage(request);
            }

            if (request.Headers.CacheControl != null && request.Headers.CacheControl.OnlyIfCached)
            {
                return CreateGatewayTimeoutResponse(request);
            }

            if (queryResult.Status == CacheStatus.Revalidate)
            {
                queryResult.ApplyConditionalHeaders(request);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                
                await _httpCache.UpdateContentAsync(response, queryResult.SelectedVariant); 
                response.Dispose();
                return queryResult.GetHttpResponseMessage(request);
                
            } 
            
            if (_httpCache.CanStore(response))
            {
                if (response.Content != null) await response.Content.LoadIntoBufferAsync();
                await _httpCache.StoreResponseAsync(response);
            }

            return response;

        }

        private HttpResponseMessage CreateGatewayTimeoutResponse(HttpRequestMessage request)
        {
            return new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                RequestMessage = request
            };
        }
    }
}