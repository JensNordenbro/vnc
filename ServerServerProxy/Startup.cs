using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VncDeviceProxy;

namespace VncDeviceProxyCloudSide
{
    public class Startup
    {
        private readonly ILogger m_Logger;
        public Startup(IConfiguration configuration, ILogger<Startup> logger)
        {
            Configuration = configuration;
            m_Logger = logger;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            
            m_Logger.LogInformation("Adding proxy proxies");
            IPAddress[] a = GetApprovedProxyAddresses();
            m_Logger.LogInformation($"Got {a.Length} addresses");
            for (int i = 0; i < a.Length; i++)
            {
                m_Logger.LogInformation($"Adding {a[i].ToString()} ");
            }


            services.Configure<ForwardedHeadersOptions>(options =>
            {

                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                m_Logger.LogInformation($"Requesting DNS");
                IPAddress[] addresses = GetApprovedProxyAddresses();
                m_Logger.LogInformation($"Got {addresses.Length} addresses");
                for (int i = 0; i < addresses.Length; i++)
                {
                    m_Logger.LogInformation($"Now Adding {addresses[i].ToString()} ");
                    options.KnownProxies.Add(addresses[i]);
                }
            });
            m_Logger.LogInformation($"Configure done");
            

            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
        }

        private IPAddress[] GetApprovedProxyAddresses() => _GetApprovedProxyAddresses().ToArray();


        private IEnumerable<IPAddress> _GetApprovedProxyAddresses()
        {
            yield return IPAddress.Parse("40.127.108.43");
            yield return IPAddress.Parse("127.0.0.1");
            yield return IPAddress.Parse("172.19.0.1");
            
            {
                IPAddress[] addresses = Dns.GetHostAddresses("nginxproxy");
                foreach (var a in addresses)
                    yield return a;
            }
            {
                IPAddress[] addresses = Dns.GetHostAddresses("serverserverproxy");
                foreach (var a in addresses)
                    yield return a;
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseForwardedHeaders();
            loggerFactory.AddConsole();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
         
            }

            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                m_Logger.LogInformation("REQUEST RECEIVED");
                if (context.WebSockets.IsWebSocketRequest)
                {
                    m_Logger.LogInformation("Accepting websocket");
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await CreateTunnel(context, webSocket);
                }
                else
                {
                    m_Logger.LogInformation("DDR WAS HERE");
                    await next();
                }
            });


            // not the best place perhaps to start a service but it is a spike :) 
            var socketServer = new TcpListener(IPAddress.Any, 5900);
            socketServer.Start();
            Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient vncClient = await socketServer.AcceptTcpClientAsync();
                    Stream vncClientStream = vncClient.GetStream();
                    m_VncClientStream.SetResult(vncClientStream);
                }
            });
        }

        TaskCompletionSource<Stream> m_VncClientStream = new TaskCompletionSource<Stream>();

        private async Task CreateTunnel(HttpContext context, WebSocket webSocket)
        {
            var vncClientStream = await m_VncClientStream.Task;
            m_VncClientStream = new TaskCompletionSource<Stream>(); // reset for next connection attempt
            // now we got the sockets/streams, lets pipe all data!
            Pipe p = new Pipe(webSocket, vncClientStream);
            await p.TunnelAsync();
        }
    }
}
