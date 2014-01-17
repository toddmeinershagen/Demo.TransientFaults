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

namespace Demo.TransientFaults.WebApi.Tests
{
    [TestFixture]
    public class ReliableHttpClientTests
    {
        private NancyHost _host;

        [SetUp]
        public void SetUp()
        {
            _host = new NancyHost(new Uri("http://localhost:1234/api/"));
            _host.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _host.Stop();
        }

        [Test]
        public async void given_one_call_to_service_it_should_fail_with_a_500_status_code()
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
    }

    public class TestModule : NancyModule
    {
        private int counter = 0;

        public TestModule()
        {
            Get["/Customers/{id}"] = (parameters) =>
                {
                    if (++counter % 3 == 0)
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
