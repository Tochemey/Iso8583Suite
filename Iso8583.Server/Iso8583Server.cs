using System;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Iso8583.Common.Iso;
using NetCore8583;

namespace Iso8583.Server
{
    /// <summary>
    ///     An instance of this class will help bootstrap an iso 8583 server
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Iso8583Server<T> : Iso8583ServerConnector<T, ServerBootstrap, ServerConfiguration> where T : IsoMessage
    {
        /// <summary>
        ///     server configuration
        /// </summary>
        private readonly ServerConfiguration _configuration;

        /// <summary>
        ///     the iso message factory
        /// </summary>
        private readonly IMessageFactory<T> _messageFactory;

        /// <summary>
        ///     server port
        /// </summary>
        private readonly int _port;

        private readonly IPEndPoint _socketAddress;

        /// <summary>
        ///     creates a new instance of <see cref="Iso8583Server{T}" />
        /// </summary>
        /// <param name="port"></param>
        /// <param name="configuration"></param>
        /// <param name="messageFactory"></param>
        public Iso8583Server(int port, ServerConfiguration configuration, IMessageFactory<T> messageFactory) : base(
            messageFactory, configuration)
        {
            _port = port;
            _messageFactory = messageFactory;
            _configuration = configuration;
            _socketAddress = new IPEndPoint(IPAddress.Any,
                port);
        }


        protected override ServerBootstrap CreateBootstrap()
        {
            var boostrap = new ServerBootstrap();
            boostrap.Group(BossEventLoopGroup, WorkerEventLoopGroup)
                .ChildOption(ChannelOption.SoKeepalive, true)
                .Channel<TcpServerSocketChannel>().LocalAddress(_socketAddress)
                .ChildHandler(new Iso8583ServerChannelInitializer<ISocketChannel, ServerBootstrap, ServerConfiguration>(
                    _configuration, ConnectorConfigurer, WorkerEventLoopGroup,
                    _messageFactory as IMessageFactory<IsoMessage>, MessageHandler
                ));
            ConfigureBootstrap(boostrap);
            boostrap.Validate();
            return boostrap;
        }

        /// <summary>
        ///     checks whether the server has started or not
        /// </summary>
        /// <returns>true when the server has started and false otherwise</returns>
        public bool IsStarted()
        {
            var channel = GetChannel();
            return channel is {Open: true};
        }

        /// <summary>
        ///     starts the iso 8583 server
        /// </summary>
        public async Task Start()
        {
            var channel = await GetBootstrap().BindAsync();
            SetChannel(channel);
        }

        /// <summary>
        ///     shutdowns the iso server gracefully
        /// </summary>
        public new async Task Shutdown()
        {
            await Stop();
            await base.Shutdown();
        }

        /// <summary>
        ///     stops the iso 8583 server
        /// </summary>
        private async Task Stop()
        {
            var channel = GetChannel();
            try
            {
                await channel.DeregisterAsync();
                await channel.CloseAsync();
            }
            catch (Exception e)
            {
                // TODO use some logging tool
                Console.WriteLine(e);
            }
        }
    }
}