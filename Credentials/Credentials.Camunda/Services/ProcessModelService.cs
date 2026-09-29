using Serilog;
using System.Reflection;
using Credentials.Models.Services;
using FiveSafesTes.Core.Constants;
using FiveSafesTes.Core.Models;
using Zeebe.Client;

namespace Credentials.Camunda.Services
{
    public class ProcessModelService : IProcessModelService
    {
        private IServicedZeebeClient _camunda;
        private readonly IConfiguration _configuration;
        private readonly DmnPath _DmnPath;

        // The editable copy of each managed DMN, keyed by file name. These live on a persistent
        // volume in a deployed stack, so they are deployed in place of the copies baked into the image.
        private readonly Dictionary<string, string> managedDmnPaths;


        public ProcessModelService(IServicedZeebeClient servicedZeebeClient, IConfiguration configuration, DmnPath DmnPath)
        {
            _camunda = servicedZeebeClient;
            _configuration = configuration;
            // Get DMN file path from configuration or use default
            _DmnPath = DmnPath;

            managedDmnPaths = DmnFiles.All.ToDictionary(
                fileName => fileName,
                fileName => ResolveDmnPath(DmnPath.Path, fileName));
        }

        /// <summary>
        /// Resolves the editable copy of a DMN file against the configured DmnPath,
        /// falling back to the location in the source tree when none is configured.
        /// </summary>
        private static string ResolveDmnPath(string? configuredPath, string fileName)
        {
            if (!string.IsNullOrEmpty(configuredPath))
            {
                // Use configured path - make it absolute if relative
                if (Path.IsPathRooted(configuredPath))
                {
                    return Path.Combine(configuredPath, fileName);
                }

                var configuredProjectDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                return Path.GetFullPath(Path.Combine(configuredProjectDirectory, configuredPath, fileName));
            }

            var projectDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            return Path.GetFullPath(Path.Combine(projectDirectory, "..", "..", "Credentials", "Credentials.Models", "ProcessModels", fileName));
        }

        public async Task DeployProcessDefinitionAndDecisionModels()
        {
            /* Testing connection */
            var gatewayAddress = _configuration["ZeebeBootstrap:Client:GatewayAddress"];

            var zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress(gatewayAddress)
                .UsePlainText()
                .Build();

            await zeebeClient.TopologyRequest().Send();
            Log.Information($"Connected to Zeebe cluster");

            // Load ProcessModels from file system
            var deployedCount = await DeployFromFileSystem();

            if (deployedCount == 0)
            {
                Log.Warning("No BPMN or DMN models found to deploy from file system.");
            }
            else
            {
                Log.Information($"Successfully deployed {deployedCount} process models total.");
            }
        }

        private async Task<int> DeployFromFileSystem()
        {
            var deployedCount = 0;
            
            // Try to find ProcessModels directory in the application directory
            var appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var processModelsPath = Path.Combine(appDirectory!, "ProcessModels");

            if (!Directory.Exists(processModelsPath))
            {
                Log.Information($"ProcessModels directory not found at: {processModelsPath}");
                return deployedCount;
            }

            var modelFiles = Directory.GetFiles(processModelsPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".bpmn", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".dmn", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (modelFiles.Any())
            {
                Log.Information($"Found {modelFiles.Count} process model files in file system to deploy.");
                
                foreach (var filePath in modelFiles)
                {
                    var deploymentFileName = Path.GetFileName(filePath);

                    // Skip the baked-in copy of a managed DMN when its editable copy exists,
                    // so admin edits win. The editable copy is deployed below instead.
                    if (managedDmnPaths.TryGetValue(deploymentFileName, out var managedPath))
                    {
                        if (File.Exists(managedPath))
                        {
                            continue;
                        }
                    }

                    try
                    {
                        using var fileStream = File.OpenRead(filePath);
                        if (await DeployModelStream(fileStream, deploymentFileName))
                        {
                            deployedCount++;
                        }
                    }
                    catch (IOException ex)
                    {
                        Log.Error($"Could not read file {filePath}: {ex.Message}");
                    }
                }
            }
            else
            {
                Log.Information($"No process model files found in: {processModelsPath}");
            }

            foreach (var (fileName, managedPath) in managedDmnPaths)
            {
                if (File.Exists(managedPath))
                {
                    using var fileStream = File.OpenRead(managedPath);
                    await _camunda.DeployModel(fileStream, fileName);
                    deployedCount++;
                }
                else
                {
                    Log.Error($"DMN file not found: {managedPath}");
                }
            }

            return deployedCount;
        }

        private async Task<bool> DeployModelStream(Stream stream, string deploymentFileName)
        {
            Log.Information($"Deploying process definition with name: {deploymentFileName}");

            try
            {
                // If this is a DMN file, deploy
                if (deploymentFileName.EndsWith(".dmn", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new StreamReader(stream);
                    var content = await reader.ReadToEndAsync();
                    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                    using var memoryStream = new MemoryStream(bytes);
                    await _camunda.DeployModel(memoryStream, deploymentFileName);
                }
                else
                {
                    // Reset stream position if possible
                    if (stream.CanSeek) stream.Position = 0;
                    await _camunda.DeployModel(stream, deploymentFileName);
                }

                Log.Information($"Successfully deployed: {deploymentFileName}");
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to deploy process definition with name: {deploymentFileName}, error: {e}");
                throw new ApplicationException("Failed to deploy process definition", e);
            }
        }
    }
}
