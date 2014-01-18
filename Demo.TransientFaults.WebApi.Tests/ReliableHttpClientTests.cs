using Nancy;
using Nancy.Hosting.Self;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using System.Net.Http.Headers;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace Demo.TransientFaults.WebApi.Tests
{
    [TestFixture]
    public class ReliableHttpClientTests
    {
        private NancyHost _host;

        [SetUp]
        public void SetUp()
        {
            Singleton.Instance.Reset();

            var config = new HostConfiguration { UrlReservations = new UrlReservations { CreateAutomatically = true} };
            _host = new NancyHost(config, new Uri("http://localhost:1234/api/"));
            
            _host.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Stop();
        }

        [Test]
        public void given_call_to_service_it_should_fail_with_a_500_status_code()
        {
            string actualContent = null;

            Action action = () =>
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var url = string.Format("http://localhost:1234/api/Customers/{0}", 1);
                    Task<HttpResponseMessage> response = client.GetAsync(url);

                    var result = response.Result;

                    Task<string> content = result.Content.ReadAsStringAsync();
                    actualContent = content.Result;

                    result.EnsureSuccessStatusCode();
                };

            action.ShouldThrow<HttpRequestException>().WithMessage("Response status code does not indicate success: 500 (Internal Server Error).");
            actualContent.Should().Be("{\"Message\":\"Something went wrong\"}");
        }

        [Test]
        public void given_call_to_service_with_incremental_retry_strategy_should_succeed_and_return_content()
        {
            var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            var retryPolicy = new RetryPolicy<HttpClientTransientErrorDetectionStrategy>(retryStrategy);

            string actualContent = null;

            Action innerAction = () =>
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var url = string.Format("http://localhost:1234/api/Customers/{0}", 1);
                    Task<HttpResponseMessage> response = client.GetAsync(url);

                    var result = response.Result;

                    Task<string> content = result.Content.ReadAsStringAsync();
                    actualContent = content.Result;

                    result.EnsureSuccessStatusCode();
                };

            Action action = () => retryPolicy.ExecuteAction(innerAction);

            action.ShouldNotThrow<HttpRequestException>();
            actualContent.Should().Be("{\"Id\":1,\"FirstName\":\"Todd\",\"LastName\":\"Meinershagen\"}");
        }
    }

    public class HttpClientTransientErrorDetectionStrategy: ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            var httpException = ex as HttpRequestException;
            return httpException != null;
        }
    }

    public class TestModule : NancyModule
    {
        public TestModule()
        {
            Get["/Customers/{id}"] = (parameters) =>
                {
                    if (++Singleton.Instance.Counter % 3 == 0)
                    {
                        return Negotiate
                            .WithStatusCode(HttpStatusCode.OK)
                            .WithModel(new Customer { Id = 1, FirstName = "Todd", LastName = "Meinershagen" });
                    }

                    return Negotiate
                        .WithStatusCode(HttpStatusCode.InternalServerError)
                        .WithModel(new Error { Message = "Something went wrong" });
                };
        }
    }

    public class Singleton
    {
        private static Singleton instance;

        private Singleton() { }

        public static Singleton Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Singleton();
                }
                return instance;
            }
        }

        public int Counter { get; set; }

        public void Reset()
        {
            Counter = 0;
        }
    }

    public class Error
    {
        public string Message { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
