namespace ChromeDevToolsGeneratorCLI
{
    using Newtonsoft.Json.Linq;

    using CSnakes.Runtime;
    using CSnakes.Runtime.Python;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.Scripting.Utils;

    /// <summary>
    /// Uses IronPython to convert the chromium protocol to other formats using the provided chromium pdl script
    /// </summary>
    /// <remarks>
    /// https://cs.chromium.org/chromium/src/third_party/inspector_protocol/
    /// </remarks>
    public class PdlConverter
    {

        public PdlConverter(string folder)
        {

            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    var home = Path.Join(folder); /* Path to your Python modules */
                    var venv = Path.Join(home, ".venv");
                    services
                        .WithPython()
                        .WithHome(home)
                        .WithVirtualEnvironment(venv)
                        .FromRedistributable(); // Download Python 3.12 and store it locally
                });

            var app = builder.Build();

            var env = app.Services.GetRequiredService<IPythonEnvironment>();

            this.logger = env.Logger;
            using (GIL.Acquire())
            {
                logger.LogInformation("Importing module {ModuleName}", "converter");
                module = Import.ImportModule("converter");
            }
        }

        private readonly PyObject module;

        private readonly ILogger<IPythonEnvironment> logger;

        public void Dispose()
        {
            logger.LogInformation("Disposing module");
            module.Dispose();
        }

        public JObject ToJson(string protocol, string fileName)
        {
            using (GIL.Acquire())
            {
                logger.LogInformation("Invoking Python function: {FunctionName}", "loads");
                using var __underlyingPythonFunc = this.module.GetAttr("loads");
                using PyObject a_pyObject = PyObject.From(protocol);
                using PyObject b_pyObject = PyObject.From(fileName);
                using var parsedProtocol = __underlyingPythonFunc.Call(a_pyObject, b_pyObject);

                logger.LogInformation("Invoking Python function: {FunctionName}", "json.dumps");
                using var jsonFunc = this.module.GetAttr("json").GetAttr("dumps");
                using var json = jsonFunc.Call(parsedProtocol);
                logger.LogInformation("Parsing JSON result");
                var jsonString = json.ToString();
                logger.LogInformation("Converting JSON string to JObject");

                return JObject.Parse(jsonString);
            }
        }
    }
}
