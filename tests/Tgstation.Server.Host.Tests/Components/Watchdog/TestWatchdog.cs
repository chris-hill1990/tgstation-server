﻿using Byond.TopicSender;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models.Internal;
using Tgstation.Server.Host.Components.Chat;
using Tgstation.Server.Host.Components.Compiler;
using Tgstation.Server.Host.Core;

namespace Tgstation.Server.Host.Components.Watchdog.Tests
{
	[TestClass]
	public sealed class TestWatchdog
	{
		[TestMethod]
		public void TestConstruction()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(null, null, null, null, null, null, null, null, null, null, null, null, default));

			var mockChat = new Mock<IChat>();
			mockChat.Setup(x => x.RegisterCommandHandler(It.IsNotNull<ICustomCommandHandler>())).Verifiable();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, null, null, null, null, null, null, null, null, null, null, null, default));

			var mockSessionControllerFactory = new Mock<ISessionControllerFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, null, null, null, null, null, null, null, null, null, null, default));

			var mockDmbFactory = new Mock<IDmbFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, null, null, null, null, null, null, null, null, null, null, default));

			var mockLogger = new Mock<ILogger<Watchdog>>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, null, null, null, null, null, null, null, null, default));

			var mockReattachInfoHandler = new Mock<IReattachInfoHandler>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, null, null, null, null, null, null, null, default));

			var mockDatabaseContextFactory = new Mock<IDatabaseContextFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, null, null, null, null, null, null, default));

			var mockByondTopicSender = new Mock<IByondTopicSender>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, null, null, null, null, null, default));

			var mockEventConsumer = new Mock<IEventConsumer>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, null, null, null, null, default));

			var mockJobManager = new Mock<IJobManager>();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, mockJobManager.Object, null, null, null, default));

			var mockRestartRegistration = new Mock<IRestartRegistration>();
			mockRestartRegistration.Setup(x => x.Dispose()).Verifiable();
			var mockServerControl = new Mock<IServerControl>();
			mockServerControl.Setup(x => x.RegisterForRestart(It.IsNotNull<IRestartHandler>())).Returns(mockRestartRegistration.Object).Verifiable();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, mockJobManager.Object, mockServerControl.Object, null, null, default));

			var mockLaunchParameters = new DreamDaemonLaunchParameters();
			Assert.ThrowsException<ArgumentNullException>(() => new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, mockJobManager.Object, mockServerControl.Object, mockLaunchParameters, null, default));

			var mockInstance = new Models.Instance();
			new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, mockJobManager.Object, mockServerControl.Object, mockLaunchParameters, mockInstance, default).Dispose();

			mockRestartRegistration.VerifyAll();
			mockServerControl.VerifyAll();
			mockChat.VerifyAll();
		}

		[TestMethod]
		public async Task TestSuccessfulLaunchAndShutdown()
		{
			var mockChat = new Mock<IChat>();
			mockChat.Setup(x => x.RegisterCommandHandler(It.IsNotNull<ICustomCommandHandler>())).Verifiable();
			var mockSessionControllerFactory = new Mock<ISessionControllerFactory>();
			var mockDmbFactory = new Mock<IDmbFactory>();
			var mockLogger = new Mock<ILogger<Watchdog>>();
			var mockReattachInfoHandler = new Mock<IReattachInfoHandler>();
			var mockDatabaseContextFactory = new Mock<IDatabaseContextFactory>();
			var mockByondTopicSender = new Mock<IByondTopicSender>();
			var mockEventConsumer = new Mock<IEventConsumer>();
			var mockJobManager = new Mock<IJobManager>();
			var mockRestartRegistration = new Mock<IRestartRegistration>();
			mockRestartRegistration.Setup(x => x.Dispose()).Verifiable();
			var mockServerControl = new Mock<IServerControl>();
			mockServerControl.Setup(x => x.RegisterForRestart(It.IsNotNull<IRestartHandler>())).Returns(mockRestartRegistration.Object).Verifiable();
			var mockLaunchParameters = new DreamDaemonLaunchParameters();
			var mockInstance = new Models.Instance();

			using (var wd = new Watchdog(mockChat.Object, mockSessionControllerFactory.Object, mockDmbFactory.Object, mockLogger.Object, mockReattachInfoHandler.Object, mockDatabaseContextFactory.Object, mockByondTopicSender.Object, mockEventConsumer.Object, mockJobManager.Object, mockServerControl.Object, mockLaunchParameters, mockInstance, default))
			using (var cts = new CancellationTokenSource())
			{
				var mockCompileJob = new Models.CompileJob();
				var mockDmbProvider = new Mock<IDmbProvider>();
				mockDmbProvider.SetupGet(x => x.CompileJob).Returns(mockCompileJob).Verifiable();
				var mDmbP = mockDmbProvider.Object;

				var infiniteTask = new TaskCompletionSource<int>().Task;

				mockDmbFactory.SetupGet(x => x.OnNewerDmb).Returns(infiniteTask);
				mockDmbFactory.Setup(x => x.LockNextDmb(2)).Returns(mDmbP).Verifiable();

				var sessionsToVerify = new List<Mock<ISessionController>>();

				var cancellationToken = cts.Token;
				mockSessionControllerFactory.Setup(x => x.LaunchNew(mockLaunchParameters, mDmbP, null, It.IsAny<bool>(), It.IsAny<bool>(), false, cancellationToken)).Returns(() =>
				{
					var mockSession = new Mock<ISessionController>();
					mockSession.SetupGet(x => x.Lifetime).Returns(infiniteTask).Verifiable();
					mockSession.SetupGet(x => x.OnReboot).Returns(infiniteTask).Verifiable();
					mockSession.SetupGet(x => x.Dmb).Returns(mDmbP).Verifiable();
					mockSession.SetupGet(x => x.LaunchResult).Returns(Task.FromResult(new LaunchResult
					{
						StartupTime = TimeSpan.FromSeconds(1)
					})).Verifiable();
					sessionsToVerify.Add(mockSession);
					return Task.FromResult(mockSession.Object);
				}).Verifiable();

				cts.CancelAfter(TimeSpan.FromSeconds(15));

				try
				{
					await wd.Launch(cancellationToken).ConfigureAwait(false);
					await wd.Terminate(false, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					cts.Cancel();
				}
				Assert.AreEqual(2, sessionsToVerify.Count);
				foreach (var I in sessionsToVerify)
					I.VerifyAll();
				mockDmbProvider.VerifyAll();
			}

			mockSessionControllerFactory.VerifyAll();
			mockDmbFactory.VerifyAll();
			mockRestartRegistration.VerifyAll();
			mockServerControl.VerifyAll();
			mockChat.VerifyAll();
		}
	}
}
