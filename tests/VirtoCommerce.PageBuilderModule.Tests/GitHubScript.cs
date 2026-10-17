using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// GitHub as a script: each request must be the next one expected — method and path-and-query — and gets
    /// that step's answer. Anything out of order, or a request too many, fails the test where it happens.
    /// </summary>
    internal sealed class GitHubScript(params (string Method, string Url, Func<HttpRequestMessage, string, HttpResponseMessage> Respond)[] script)
        : HttpMessageHandler, IHttpClientFactory
    {
        private int _step;

        public int Requests => _step;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.True(_step < script.Length, $"Unexpected request #{_step + 1}: {request.Method} {request.RequestUri}");
            var (method, url, respond) = script[_step++];
            Assert.Equal(method, request.Method.Method);
            Assert.Equal(url, request.RequestUri!.PathAndQuery.TrimStart('/'));
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request, body);
        }

        public void AssertDone() => Assert.Equal(script.Length, _step);

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("https://api.github.test/") };

        public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("{}") };
    }
}
