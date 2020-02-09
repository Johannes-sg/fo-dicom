﻿using System;
using System.Text;
using FellowOakDicom.Log;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FellowOakDicom.Network
{
    public interface IDicomServerFactory
    {
        /// <summary>
        /// Creates a DICOM server object.
        /// </summary>
        /// <typeparam name="T">DICOM service that the server should manage.</typeparam>
        /// <param name="port">Port to listen to.</param>
        /// <param name="certificateName">Certificate name for authenticated connections.</param>
        /// <param name="configureOptions">Service options.</param>
        /// <param name="fallbackEncoding">Fallback encoding.</param>
        /// <param name="logger">Logger, if null default logger will be applied.</param>
        /// <returns>An instance of <see cref="DicomServer{T}"/>, that starts listening for connections in the background.</returns>
        IDicomServer Create<T>(
            int port,
            string certificateName = null,
            Action<DicomServiceOptions> configureOptions = null,
            Encoding fallbackEncoding = null,
            Logger logger = null) where T : DicomService, IDicomServiceProvider;
        
        /// <summary>
        /// Creates a DICOM server object.
        /// </summary>
        /// <typeparam name="T">DICOM service that the server should manage.</typeparam>
        /// <typeparam name="TServer">Concrete DICOM server type to be returned.</typeparam>
        /// <param name="ipAddress">IP address(es) to listen to. Value <code>null</code> applies default, IPv4Any.</param>
        /// <param name="port">Port to listen to.</param>
        /// <param name="userState">Optional optional parameters.</param>
        /// <param name="certificateName">Certificate name for authenticated connections.</param>
        /// <param name="configureOptions">Service options.</param>
        /// <param name="fallbackEncoding">Fallback encoding.</param>
        /// <param name="logger">Logger, if null default logger will be applied.</param>
        /// <returns>An instance of <typeparamref name="TServer"/>, that starts listening for connections in the background.</returns>
        IDicomServer Create<T, TServer>(
            string ipAddress,
            int port,
            object userState = null,
            string certificateName = null,
            Action<DicomServiceOptions> configureOptions = null,
            Encoding fallbackEncoding = null,
            ILogger logger = null) where T : DicomService, IDicomServiceProvider where TServer : IDicomServer<T>;
    }
    
    public class DicomServerFactory : IDicomServerFactory
    {
        private readonly IServiceScopeFactory  _serviceScopeFactory;
        private readonly IDicomServerRegistry _dicomServerRegistry;
        private readonly IOptions<DicomServiceOptions> _defaultServiceOptions;

        public DicomServerFactory(
            IServiceScopeFactory  serviceScopeFactory,
            IDicomServerRegistry dicomServerRegistry,
            IOptions<DicomServiceOptions> defaultServiceOptions)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _dicomServerRegistry = dicomServerRegistry ?? throw new ArgumentNullException(nameof(dicomServerRegistry));
            _defaultServiceOptions = defaultServiceOptions ?? throw new ArgumentNullException(nameof(defaultServiceOptions));
        }

        public IDicomServer Create<T>(
            int port, 
            string certificateName = null,
            Action<DicomServiceOptions> configureOptions = null, 
            Encoding fallbackEncoding = null, 
            Logger logger = null)
            where T : DicomService, IDicomServiceProvider 
            => Create<T, DicomServer<T>>(NetworkManager.IPv4Any, port, null, certificateName, configureOptions, fallbackEncoding, logger);

        public IDicomServer Create<TServiceProvider, TServer>(
            string ipAddress, 
            int port, 
            object userState = null, 
            string certificateName = null, 
            Action<DicomServiceOptions> configureOptions = null, 
            Encoding fallbackEncoding = null, 
            ILogger logger = null) where TServiceProvider : DicomService, IDicomServiceProvider where TServer : IDicomServer<TServiceProvider>
        {
            var dicomServerScope = _serviceScopeFactory.CreateScope();
            
            if (!_dicomServerRegistry.IsAvailable(ipAddress, port))
            {
                throw new DicomNetworkException("There is already a DICOM server registered on port: {0}", port);
            }

            var server = dicomServerScope.ServiceProvider.GetRequiredService<TServer>();
            
            if (logger != null)
            {
                server.Logger = logger;
            }

            var serviceOptions = _defaultServiceOptions.Value;
            
            configureOptions?.Invoke(serviceOptions);
            
            var runner = server.StartAsync(ipAddress, port, certificateName, fallbackEncoding, serviceOptions, userState);

            var registration = _dicomServerRegistry.Register(server, runner);

            server.Registration = registration;
            server.ServiceScope = dicomServerScope;

            return server;
        }
    }
}