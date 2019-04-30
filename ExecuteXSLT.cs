using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Xml.Xsl;
using System.Xml;

namespace IntegrationAsAFunction.Functions
{
    public static class ExecuteXSLT
    {
        [FunctionName("ExecuteXSLT")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string xml = req.Query["xml"];
            xml = xml ?? data?.xml;

            var xsl = new XslCompiledTransform();
            var mapsFolder = Path.GetFullPath(Path.Combine(GetScriptPath(), "maps"));
            var xsltFullPath = Path.GetFullPath(Path.Combine(mapsFolder, $"{name}.xslt"));

            log.LogInformation("Creating settings...");
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.IgnoreWhitespace = true;
            settings.IgnoreComments = true;
            log.LogInformation("Creating reader...");
            XmlReader reader = XmlReader.Create(xsltFullPath, settings);
            log.LogInformation("Creating xsl settings");
            XsltSettings sets = new XsltSettings(true, false);
            log.LogInformation("Creating xsl url resolver");
            var resolver = new XmlUrlResolver();
            log.LogInformation("Loading the XSL");
            xsl.Load(reader, sets, resolver);
            string result = null;

            if(!String.IsNullOrWhiteSpace(xml)) {
                log.LogInformation("Found XML");
                using (StringReader sri = new StringReader(xml)) // xmlInput is a string that contains xml
                {
                    using (XmlReader xri = XmlReader.Create(sri))
                    {
                        using (StringWriter sw = new StringWriter())
                        using (XmlWriter xwo = XmlWriter.Create(sw, xsl.OutputSettings)) // use OutputSettings of xsl, so it can be output as HTML
                        {
                            log.LogInformation("Transforming: {xml}", xml);
                            xsl.Transform(xri, xwo);
                            result = sw.ToString();
                        }
                        log.LogInformation("Result: {result}", result);
                    }
                }
            }

            return name != null
                ? (ActionResult)new OkObjectResult($"{result}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        #region GETS
        private static string GetScriptPath()
        => Path.Combine(GetEnvironmentVariable("HOME"), @"site\wwwroot");

        private static string GetEnvironmentVariable(string name)
        => System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        #endregion
    }
}
