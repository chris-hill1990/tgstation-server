﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Host.Configuration;
using Tgstation.Server.Host.IO;

namespace Tgstation.Server.Host.Core.Tests
{
	[TestClass]
	public sealed class TestSetupWizard
	{
		[TestMethod]
		public void TestConstructionThrows()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(null, null, null, null, null, null, null));
			var mockIOManager = new Mock<IIOManager>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, null, null, null, null, null, null));
			var mockConsole = new Mock<IConsole>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, mockConsole.Object, null, null, null, null, null));
			var mockHostingEnvironment = new Mock<IHostingEnvironment>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, mockConsole.Object, mockHostingEnvironment.Object, null, null, null, null));
			var mockApplication = new Mock<IApplication>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, mockConsole.Object, mockHostingEnvironment.Object, mockApplication.Object, null, null, null));
			var mockDBConnectionFactory = new Mock<IDBConnectionFactory>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, mockConsole.Object, mockHostingEnvironment.Object, mockApplication.Object, mockDBConnectionFactory.Object, null, null));
			var mockLogger = new Mock<ILogger<SetupWizard>>();
			Assert.ThrowsException<ArgumentNullException>(() => new SetupWizard(mockIOManager.Object, mockConsole.Object, mockHostingEnvironment.Object, mockApplication.Object, mockDBConnectionFactory.Object, mockLogger.Object, null));
		}
		
		[TestMethod]
		public async Task TestWithUserStupiditiy()
		{
			var mockIOManager = new Mock<IIOManager>();
			var mockConsole = new Mock<IConsole>();
			var mockHostingEnvironment = new Mock<IHostingEnvironment>();
			var mockApplication = new Mock<IApplication>();
			var mockDBConnectionFactory = new Mock<IDBConnectionFactory>();
			var mockLogger = new Mock<ILogger<SetupWizard>>();
			var mockGeneralConfigurationOptions = new Mock<IOptions<GeneralConfiguration>>();

			var testGeneralConfig = new GeneralConfiguration
			{
				SetupWizardMode = SetupWizardMode.Never
			};
			mockGeneralConfigurationOptions.SetupGet(x => x.Value).Returns(testGeneralConfig).Verifiable();

			var wizard = new SetupWizard(mockIOManager.Object, mockConsole.Object, mockHostingEnvironment.Object, mockApplication.Object, mockDBConnectionFactory.Object, mockLogger.Object, mockGeneralConfigurationOptions.Object);

			Assert.IsFalse(await wizard.CheckRunWizard(default).ConfigureAwait(false));

			testGeneralConfig.SetupWizardMode = SetupWizardMode.Force;
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => wizard.CheckRunWizard(default)).ConfigureAwait(false);

			testGeneralConfig.SetupWizardMode = SetupWizardMode.Only;
			await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => wizard.CheckRunWizard(default)).ConfigureAwait(false);

			mockConsole.SetupGet(x => x.Available).Returns(true).Verifiable();
			mockIOManager.Setup(x => x.FileExists(It.IsNotNull<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true)).Verifiable();
			mockIOManager.Setup(x => x.ReadAllBytes(It.IsNotNull<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(Encoding.UTF8.GetBytes("cucked"))).Verifiable();
			mockIOManager.Setup(x => x.WriteAllBytes(It.IsNotNull<string>(), It.IsNotNull<byte[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
			
			var mockSuccessCommand = new Mock<DbCommand>();
			mockSuccessCommand.Setup(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(0)).Verifiable();
			mockSuccessCommand.Setup(x => x.ExecuteScalarAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<object>("1.2.3")).Verifiable();
			var mockFailCommand = new Mock<DbCommand>();
			mockFailCommand.Setup(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>())).Throws(new Exception()).Verifiable();

			void SetDbCommandCreator(Mock<DbConnection> mock, Func<DbCommand> creator) => mock.Protected().Setup<DbCommand>("CreateDbCommand").Returns(creator).Verifiable();

			var mockGoodDbConnection = new Mock<DbConnection>();
			mockGoodDbConnection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
			SetDbCommandCreator(mockGoodDbConnection, () => mockSuccessCommand.Object);

			var mockBadDbConnection = new Mock<DbConnection>();
			mockBadDbConnection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).Throws(new Exception()).Verifiable();
			var invokeTimes = 0;
			var mockUglyDbConnection = new Mock<DbConnection>();
			SetDbCommandCreator(mockUglyDbConnection, () =>
			{
				if (invokeTimes < 2)
				{
					++invokeTimes;
					return mockSuccessCommand.Object;
				}
				else
					return mockFailCommand.Object;
			});

			mockDBConnectionFactory.Setup(x => x.CreateConnection(It.IsAny<string>(), DatabaseType.SqlServer)).Returns(mockBadDbConnection.Object).Verifiable();
			mockDBConnectionFactory.Setup(x => x.CreateConnection(It.IsAny<string>(), DatabaseType.MariaDB)).Returns(mockGoodDbConnection.Object).Verifiable();
			mockDBConnectionFactory.Setup(x => x.CreateConnection(It.IsAny<string>(), DatabaseType.MySql)).Returns(mockUglyDbConnection.Object).Verifiable();

			var finalInputSequence = new List<string>()
			{
				//first run, just say no to the force prompt after testing it
				"fake",
				"n",
				//second run say yes to the force prompt
				"y",
				//first normal run
				"bad port number",
				"0",
				"666",
				"FakeDBType",
				nameof(DatabaseType.SqlServer),
				"this isn't validated",
				"nor is this",
				"no",
			};
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				//test winauth
				finalInputSequence.Add("yes");
			else
				finalInputSequence.AddRange(new List<string>
				{
					"username",
					"password"
				});
			finalInputSequence.AddRange(new List<string>
			{
				//sql server will always fail so reconfigure with maria
				nameof(DatabaseType.MariaDB),
				"bleh",
				"blah",
				"NO",
				"user",
				"pass",
				//general config
				"four",
				"-12",
				"16",
				"eight",
				"-27",
				"5000",
				"fake token",
				//logging config
				"no",
				//saved, now for second run
				//this time use defaults amap
				String.Empty,
				//test MySQL errors
				nameof(DatabaseType.MySql),
				String.Empty,
				String.Empty,
				"DbName",
				"n",
				"user",
				"pass",
				//general config
				String.Empty,
				String.Empty,
				String.Empty,
				//logging config
				"y",
				"not actually verified because lol mocks /../!@#$%^&*()/..///.",
				"Warning",
				String.Empty,
				//third run, we already hit all the code coverage so just get through it
				String.Empty,
				nameof(DatabaseType.MariaDB),
				String.Empty,
				"dbname",
				"y",
				"user",
				"pass",
				String.Empty,
				String.Empty,
				String.Empty,
				"y",
				"will faile",
				String.Empty,
				String.Empty,
				"fake",
				"None",
				"Critical"
			});

			var inputPos = 0;

			mockApplication.SetupGet(x => x.VersionPrefix).Returns("sumfuk").Verifiable();

			mockConsole.Setup(x => x.PressAnyKeyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();
			mockConsole.Setup(x => x.ReadLineAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(() =>
			{
				if (inputPos == finalInputSequence.Count)
					Assert.Fail("Exhausted input sequence!");
				var res = finalInputSequence[inputPos++];
				return Task.FromResult(res);
			}).Verifiable();
			mockConsole.Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask).Verifiable();

			Assert.IsFalse(await wizard.CheckRunWizard(default).ConfigureAwait(false));
			//first real run
			Assert.IsTrue(await wizard.CheckRunWizard(default).ConfigureAwait(false));

			//second run
			mockIOManager.Setup(x => x.ReadAllBytes(It.IsNotNull<string>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(Encoding.UTF8.GetBytes(String.Empty))).Verifiable();
			Assert.IsTrue(await wizard.CheckRunWizard(default).ConfigureAwait(false));
		
			//third run
			testGeneralConfig.SetupWizardMode = SetupWizardMode.Autodetect;
			mockIOManager.Setup(x => x.WriteAllBytes(It.IsNotNull<string>(), It.IsNotNull<byte[]>(), It.IsAny<CancellationToken>())).Throws(new Exception()).Verifiable();
			var firstRun = true;
			mockIOManager.Setup(x => x.CreateDirectory(It.IsNotNull<string>(), It.IsAny<CancellationToken>())).Returns(() =>
			{
				if (firstRun)
				{
					firstRun = false;
					throw new Exception();
				}
				return Task.CompletedTask;
			}).Verifiable();

			await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => wizard.CheckRunWizard(default)).ConfigureAwait(false);

			Assert.AreEqual(finalInputSequence.Count, inputPos);
			mockFailCommand.VerifyAll();
			mockSuccessCommand.VerifyAll();
			mockIOManager.VerifyAll();
			mockGeneralConfigurationOptions.VerifyAll();
			mockConsole.VerifyAll();
			mockGoodDbConnection.VerifyAll();
			mockBadDbConnection.VerifyAll();
			mockUglyDbConnection.VerifyAll();
			mockDBConnectionFactory.VerifyAll();
			mockApplication.VerifyAll();
		}
	}
}
