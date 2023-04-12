using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using System.Net.Http;
using Microsoft.ApplicationInsights.DataContracts;
using System.Net.Http.Headers;

namespace AI.AvailabilityTests
{
    public class CheckAvailability
    {
        private readonly TelemetryClient _telemetryClient;
        private readonly IHttpClientFactory _httpClientFactory;
        public CheckAvailability(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _telemetryClient = new TelemetryClient(new TelemetryConfiguration(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")));
         
        }


        [FunctionName("CheckAvailability")]
        public async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            string endpoint = req.Query["endpoint"];
            string method = req.Query["method"];
            string testName = req.Query["testName"];
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            string location = Environment.GetEnvironmentVariable("REGION_NAME");
            string operationId = Guid.NewGuid().ToString("N");
            var availabilityTelemetry = new AvailabilityTelemetry
            {
                Id = operationId,
                Name = testName,
                RunLocation = location,
                Success = false
            };

            try
            {
                await ExecuteTestAsync(endpoint, method, body, log);
                availabilityTelemetry.Success = true;
            }
            catch (Exception ex)
            {
                availabilityTelemetry.Message = ex.Message;

                var exceptionTelemetry = new ExceptionTelemetry(ex);
                exceptionTelemetry.Context.Operation.Id = operationId;
                exceptionTelemetry.Properties.Add("TestName", testName);
                exceptionTelemetry.Properties.Add("TestLocation", location);
                _telemetryClient.TrackException(exceptionTelemetry);
            }
            finally
            {
                _telemetryClient.TrackAvailability(availabilityTelemetry);
                _telemetryClient.Flush();
            }
        }

        public async Task ExecuteTestAsync(string endpoint, string method, string body, ILogger log)
        {
            log.LogInformation("RunAvailabilityTestAsync - Started.");
            var httpClient = _httpClientFactory.CreateClient();
            HttpResponseMessage httpResponseMessage = null;
            if (method.Equals("POST"))
                httpResponseMessage = await httpClient.PostAsJsonAsync(endpoint, body);
            else
                httpResponseMessage = await httpClient.GetAsync(endpoint);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                log.LogInformation("RunAvailabilityTestAsync - Success.");
            }
            else
            {
                log.LogError("RunAvailabilityTestAsync - Failed.");
                throw new Exception("RunAvailabilityTestAsync - Failed.");
            }



        }
    }
}
