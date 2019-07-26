namespace CSharpModule
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System.Collections.Generic;     // For KeyValuePair<>
    using Microsoft.Azure.Devices.Shared; // For TwinCollection
    using Newtonsoft.Json;                // For JsonConvert
    using IoTLabClassLibrary.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.AspNetCore.SignalR.Client;

    class Program
    {
        static int counter;
        static int temperatureThreshold { get; set; } = 25;

        private static readonly ILogger logger = CreateLogger("Program");

    
        static void Main(string[] args)
        {
// Testing SignalR
         /*   var cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => MainAsync(cancellationTokenSource.Token).GetAwaiter().GetResult(), cancellationTokenSource.Token);

            Console.WriteLine("Press Enter to Exit ...");
            Console.ReadLine();

            cancellationTokenSource.Cancel();*/

                  Init().Wait();

                  // Wait until the app unloads or is cancelled
                  var cts = new CancellationTokenSource();
                  AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
                  Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
                  WhenCancelled(cts.Token).Wait();
        }
        //For SignalR testing...
        private static async Task MainAsync(CancellationToken cancellationToken)
        {
            var hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/sensor")
                .Build();

            await hubConnection.StartAsync();
            Console.WriteLine("Hub connection initialized.");

            // Initialize a new Random Number Generator:
            Random rnd = new Random();

            double value = 0.0d;

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(250, cancellationToken);

                // Generate the value to Broadcast to Clients:
                value = Math.Min(Math.Max(value + (0.1 - rnd.NextDouble() / 5.0), -1), 1);

                // Create the Measurement with a Timestamp assigned:
                var measurement = new Measurement() { Timestamp = DateTime.UtcNow, Value = value };

                // Log informations:
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    Console.WriteLine("Broadcasting Measurement to Clients ({0})", measurement);
                }

                // Finally send the value:
                Console.WriteLine("Broadcasting Measurement to Clients ({0})", measurement);
                await hubConnection.InvokeAsync("Broadcast", "Sensor", measurement, cancellationToken);
            }

            await hubConnection.DisposeAsync();
        }


        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
         
            AmqpTransportSettings amqpSetting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
            ITransportSettings[] settings = { amqpSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Read the TemperatureThreshold value from the module twin's desired properties
            var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, ioTHubModuleClient);

            // Attach a callback for updates to the module twin's desired properties.
            await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

            // Register a callback for messages that are received by the module.
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", FilterMessages, ioTHubModuleClient);

           
        }

        static Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                if (desiredProperties["TemperatureThreshold"] != null)
                    temperatureThreshold = desiredProperties["TemperatureThreshold"];

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> FilterMessages(Message message, object userContext )
        {
            
            var counterValue = Interlocked.Increment(ref counter);
            double value = 0.0d;
            try
            {
                ModuleClient moduleClient = (ModuleClient)userContext;
                var messageBytes = message.GetBytes();
                var messageString = Encoding.UTF8.GetString(messageBytes);
                Console.WriteLine($"Received message {counterValue}: [{messageString}]");

                // Get the message body.
                var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString);
                value = messageBody.machine.temperature;
                var measurement = new Measurement() { Timestamp = DateTime.UtcNow, Value = value };

                // Log informations:
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    Console.WriteLine("Broadcasting Measurement to Clients ({0})", measurement);
                }

                // Finally send the value:
                //   await hubConnection.InvokeAsync("Broadcast", "Sensor", measurement);

                if (messageBody != null && messageBody.machine.temperature > temperatureThreshold)
                {
                    Console.WriteLine($"Machine temperature {messageBody.machine.temperature} " +
                        $"exceeds threshold {temperatureThreshold}");
                    var filteredMessage = new Message(messageBytes);
                    foreach (KeyValuePair<string, string> prop in message.Properties)
                    {
                        filteredMessage.Properties.Add(prop.Key, prop.Value);
                    }

                    filteredMessage.Properties.Add("MessageType", "Alert");
                    await moduleClient.SendEventAsync("output1", filteredMessage);

                }

                // Indicate that the message treatment is completed.
                return MessageResponse.Completed;
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error in sample: {0}", exception);
                }
                // Indicate that the message treatment is not completed.
                var moduleClient = (ModuleClient)userContext;
                return MessageResponse.Abandoned;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error in sample: {0}", ex.Message);
                // Indicate that the message treatment is not completed.
                ModuleClient moduleClient = (ModuleClient)userContext;
                return MessageResponse.Abandoned;
            }
        }
        private static ILogger CreateLogger(string loggerName)
        {
            return new LoggerFactory()
               // .AddConsole(LogLevel.Trace)
                .CreateLogger(loggerName);
        }

    }
}
