﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api.Models;
using Tgstation.Server.Host.Core;
using Tgstation.Server.Host.IO;
using Tgstation.Server.Host.Security;

namespace Tgstation.Server.Host.Components.StaticFiles
{
	/// <inheritdoc />
	sealed class Configuration : IConfiguration
	{
		const string CodeModificationsSubdirectory = "CodeModifications";
		const string EventScriptsSubdirectory = "EventScripts";
		const string GameStaticFilesSubdirectory = "GameStaticFiles";

		const string CodeModificationsHeadFile = "HeadInclude.dm";
		const string CodeModificationsTailFile = "TailInclude.dm";

		static readonly IReadOnlyDictionary<EventType, string> EventTypeScriptFileNameMap = new Dictionary<EventType, string>
		{
			{ EventType.CompileStart, "PreCompile" },
			{ EventType.CompileComplete, "PostCompile" },
			{ EventType.RepoPreSynchronize, "PreSynchronize" }
		};

		static readonly string SystemScriptFileExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "bat" : "sh";

		/// <summary>
		/// The <see cref="IIOManager"/> for <see cref="Configuration"/>
		/// </summary>
		readonly IIOManager ioManager;

		/// <summary>
		/// The <see cref="ISynchronousIOManager"/> for <see cref="Configuration"/>
		/// </summary>
		readonly ISynchronousIOManager synchronousIOManager;

		/// <summary>
		/// The <see cref="ISymlinkFactory"/> for <see cref="Configuration"/>
		/// </summary>
		readonly ISymlinkFactory symlinkFactory;

		/// <summary>
		/// The <see cref="IProcessExecutor"/> for <see cref="Configuration"/>
		/// </summary>
		readonly IProcessExecutor processExecutor;

		/// <summary>
		/// The <see cref="IPostWriteHandler"/> for <see cref="Configuration"/>
		/// </summary>
		readonly IPostWriteHandler postWriteHandler;

		/// <summary>
		/// The <see cref="ILogger"/> for <see cref="Configuration"/>
		/// </summary>
		readonly ILogger<Configuration> logger;

		/// <summary>
		/// The <see cref="SemaphoreSlim"/> for <see cref="Configuration"/>
		/// </summary>
		readonly SemaphoreSlim semaphore;

		/// <summary>
		/// Construct <see cref="Configuration"/>
		/// </summary>
		/// <param name="ioManager">The value of <see cref="ioManager"/></param>
		/// <param name="synchronousIOManager">The value of <see cref="synchronousIOManager"/></param>
		/// <param name="symlinkFactory">The value of <see cref="symlinkFactory"/></param>
		/// <param name="processExecutor">The value of <see cref="processExecutor"/></param>
		/// <param name="postWriteHandler">The value of <see cref="postWriteHandler"/></param>
		/// <param name="logger">The value of <see cref="logger"/></param>
		public Configuration(IIOManager ioManager, ISynchronousIOManager synchronousIOManager, ISymlinkFactory symlinkFactory, IProcessExecutor processExecutor, IPostWriteHandler postWriteHandler, ILogger<Configuration> logger)
		{
			this.ioManager = ioManager ?? throw new ArgumentNullException(nameof(ioManager));
			this.synchronousIOManager = synchronousIOManager ?? throw new ArgumentNullException(nameof(synchronousIOManager));
			this.symlinkFactory = symlinkFactory ?? throw new ArgumentNullException(nameof(symlinkFactory));
			this.processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
			this.postWriteHandler = postWriteHandler ?? throw new ArgumentNullException(nameof(postWriteHandler));
			this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

			semaphore = new SemaphoreSlim(1);
		}

		/// <inheritdoc />
		public void Dispose() => semaphore.Dispose();

		/// <summary>
		/// Ensures standard configuration directories exist
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task"/> representing the running operation</returns>
		Task EnsureDirectories(CancellationToken cancellationToken) => Task.WhenAll(ioManager.CreateDirectory(CodeModificationsSubdirectory, cancellationToken), ioManager.CreateDirectory(EventScriptsSubdirectory, cancellationToken), ioManager.CreateDirectory(GameStaticFilesSubdirectory, cancellationToken));

		/// <inheritdoc />
		public async Task<ServerSideModifications> CopyDMFilesTo(string dmeFile, string destination, CancellationToken cancellationToken)
		{

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
			{
				await EnsureDirectories(cancellationToken).ConfigureAwait(false);

				//just assume no other fs race conditions here
				var dmeExistsTask = ioManager.FileExists(ioManager.ConcatPath(CodeModificationsSubdirectory, dmeFile), cancellationToken);
				var headFileExistsTask = ioManager.FileExists(ioManager.ConcatPath(CodeModificationsSubdirectory, CodeModificationsHeadFile), cancellationToken);
				var tailFileExistsTask = ioManager.FileExists(ioManager.ConcatPath(CodeModificationsSubdirectory, CodeModificationsTailFile), cancellationToken);
				var copyTask = ioManager.CopyDirectory(CodeModificationsSubdirectory, destination, null, cancellationToken);

				await Task.WhenAll(dmeExistsTask, headFileExistsTask, tailFileExistsTask, copyTask).ConfigureAwait(false);

				if (!dmeExistsTask.Result && !headFileExistsTask.Result && !tailFileExistsTask.Result)
					return null;

				if (dmeExistsTask.Result)
					return new ServerSideModifications(null, null, true);

				if (!headFileExistsTask.Result && !tailFileExistsTask.Result)
					return null;

				string IncludeLine(string filePath) => String.Format(CultureInfo.InvariantCulture, "#include \"{0}\"", filePath);
			
				return new ServerSideModifications(headFileExistsTask.Result ? IncludeLine(CodeModificationsHeadFile) : null, tailFileExistsTask.Result ? IncludeLine(CodeModificationsTailFile) : null, false);
			}
		}

		string ValidateConfigRelativePath(string configurationRelativePath)
		{
			var nullOrEmptyCheck = String.IsNullOrEmpty(configurationRelativePath);
			if (nullOrEmptyCheck)
				configurationRelativePath = ".";
			if (configurationRelativePath[0] == Path.DirectorySeparatorChar || configurationRelativePath[0] == Path.AltDirectorySeparatorChar)
				configurationRelativePath = '.' + configurationRelativePath;
			var resolved = ioManager.ResolvePath(configurationRelativePath);
			var local = !nullOrEmptyCheck ? ioManager.ResolvePath(".") : null;
			if (!nullOrEmptyCheck && resolved.Length < local.Length) //.. fuccbois
				throw new InvalidOperationException("Attempted to access file outside of configuration manager!");
			return resolved;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<ConfigurationFile>> ListDirectory(string configurationRelativePath, ISystemIdentity systemIdentity, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);
			var path = ValidateConfigRelativePath(configurationRelativePath);

			if (configurationRelativePath == null)
				configurationRelativePath = "/";

			List<ConfigurationFile> result = new List<ConfigurationFile>();

			void ListImpl()
			{
				var enumerator = synchronousIOManager.GetDirectories(path, cancellationToken);
				try
				{
					result.AddRange(enumerator.Select(x => new ConfigurationFile
					{
						IsDirectory = true,
						Path = ioManager.ConcatPath(configurationRelativePath, x),
					}));
				}
				catch (IOException e)
				{
					logger.LogDebug("IOException while writing {0}: {1}", path, e);
					result = null;
					return;
				}
				enumerator = synchronousIOManager.GetFiles(path, cancellationToken);
				result.AddRange(enumerator.Select(x => new ConfigurationFile
				{
					IsDirectory = false,
					Path = ioManager.ConcatPath(configurationRelativePath, x),
				}));
			}

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
				if (systemIdentity == null)
					ListImpl();
				else
					await systemIdentity.RunImpersonated(ListImpl, cancellationToken).ConfigureAwait(false);

			return result;
		}

		/// <inheritdoc />
		public async Task<ConfigurationFile> Read(string configurationRelativePath, ISystemIdentity systemIdentity, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);
			var path = ValidateConfigRelativePath(configurationRelativePath);

			ConfigurationFile result = null;

			void ReadImpl()
			{
				lock (this)
					try
					{
						var content = synchronousIOManager.ReadFile(path);
						string sha1String;
#pragma warning disable CA5350 // Do not use insecure cryptographic algorithm SHA1.
						using (var sha1 = new SHA1Managed())
#pragma warning restore CA5350 // Do not use insecure cryptographic algorithm SHA1.
							sha1String = String.Join("", sha1.ComputeHash(content).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
						result = new ConfigurationFile
						{
							Content = content,
							IsDirectory = false,
							LastReadHash = sha1String,
							AccessDenied = false,
							Path = configurationRelativePath
						};
					}
					catch (UnauthorizedAccessException)
					{
						//this happens on windows, dunno about linux
						bool isDirectory;
						try
						{
							isDirectory = synchronousIOManager.IsDirectory(path);
						}
						catch
						{
							isDirectory = false;
						}

						result = new ConfigurationFile
						{
							Path = configurationRelativePath
						};
						if (!isDirectory)
							result.AccessDenied = true;
						else
							result.IsDirectory = true;
					}
			}

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
				if (systemIdentity == null)
					await Task.Factory.StartNew(ReadImpl, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
				else
					await systemIdentity.RunImpersonated(ReadImpl, cancellationToken).ConfigureAwait(false);

			return result;
		}

		/// <inheritdoc />
		public async Task SymlinkStaticFilesTo(string destination, CancellationToken cancellationToken)
		{
			async Task SymlinkBase(bool files)
			{
				Task<IReadOnlyList<string>> task;
				if (files)
					task = ioManager.GetFiles(GameStaticFilesSubdirectory, cancellationToken);
				else
					task = ioManager.GetDirectories(GameStaticFilesSubdirectory, cancellationToken);
				var entries = await task.ConfigureAwait(false);

				await Task.WhenAll(task.Result.Select(async x =>
				{
					var destPath = ioManager.ConcatPath(destination, ioManager.GetFileName(x));
					logger.LogTrace("Symlinking {0} to {1}...", x, destPath);
					var fileExistsTask = ioManager.FileExists(destPath, cancellationToken);
					if (await ioManager.DirectoryExists(destPath, cancellationToken).ConfigureAwait(false))
						await ioManager.DeleteDirectory(destPath, cancellationToken).ConfigureAwait(false);
					var fileExists = await fileExistsTask.ConfigureAwait(false);
					if (fileExists)
						await ioManager.DeleteFile(destPath, cancellationToken).ConfigureAwait(false);
					await symlinkFactory.CreateSymbolicLink(ioManager.ResolvePath(x), ioManager.ResolvePath(destPath), cancellationToken).ConfigureAwait(false);
				})).ConfigureAwait(false);
			}

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
			{
				await EnsureDirectories(cancellationToken).ConfigureAwait(false);
				await Task.WhenAll(SymlinkBase(true), SymlinkBase(false)).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public async Task<ConfigurationFile> Write(string configurationRelativePath, ISystemIdentity systemIdentity, byte[] data, string previousHash, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);
			var path = ValidateConfigRelativePath(configurationRelativePath);

			ConfigurationFile result = null;

			void WriteImpl()
			{
				lock (this)
					try
					{
						var fileHash = previousHash;
						var success = synchronousIOManager.WriteFileChecked(path, data, ref fileHash, cancellationToken);
						if (!success)
							return;
						result = new ConfigurationFile
						{
							Content = data,
							IsDirectory = false,
							LastReadHash = fileHash,
							AccessDenied = false,
							Path = configurationRelativePath
						};
					}
					catch (UnauthorizedAccessException)
					{
						//this happens on windows, dunno about linux
						bool isDirectory;
						try
						{
							isDirectory = synchronousIOManager.IsDirectory(path);
						}
						catch
						{
							isDirectory = false;
						}

						result = new ConfigurationFile
						{
							Path = configurationRelativePath
						};
						if (!isDirectory)
							result.AccessDenied = true;
						else
							result.IsDirectory = true;
					}
			}

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
				if (systemIdentity == null)
					await Task.Factory.StartNew(WriteImpl, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
				else
					await systemIdentity.RunImpersonated(WriteImpl, cancellationToken).ConfigureAwait(false);

			return result;
		}

		/// <inheritdoc />
		public async Task<bool> CreateDirectory(string configurationRelativePath, ISystemIdentity systemIdentity, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);
			var path = ValidateConfigRelativePath(configurationRelativePath);

			bool? result = null;
			void DoCreate() => result = synchronousIOManager.CreateDirectory(path, cancellationToken);

			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
				if (systemIdentity == null)
					await Task.Factory.StartNew(DoCreate, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
				else
					await systemIdentity.RunImpersonated(DoCreate, cancellationToken).ConfigureAwait(false);

			return result.Value;
		}

		/// <inheritdoc />
		public Task StartAsync(CancellationToken cancellationToken) => EnsureDirectories(cancellationToken);

		/// <inheritdoc />
		public Task StopAsync(CancellationToken cancellationToken) => EnsureDirectories(cancellationToken);

		/// <inheritdoc />
		public async Task<bool> HandleEvent(EventType eventType, IEnumerable<string> parameters, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);

			if (!EventTypeScriptFileNameMap.TryGetValue(eventType, out var scriptName))
				return true;

			//always execute in serial
			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
			{
				var files = await ioManager.GetFilesWithExtension(EventScriptsSubdirectory, SystemScriptFileExtension, cancellationToken).ConfigureAwait(false);
				var resolvedScriptsDir = ioManager.ResolvePath(EventScriptsSubdirectory);

				foreach (var I in files.Select(x => ioManager.GetFileName(x)).Where(x => x.StartsWith(scriptName, StringComparison.Ordinal)))
					using (var script = processExecutor.LaunchProcess(ioManager.ConcatPath(resolvedScriptsDir, I), resolvedScriptsDir, String.Join(' ', parameters), noShellExecute: true))
					using (cancellationToken.Register(() => script.Terminate()))
					{
						var exitCode = await script.Lifetime.ConfigureAwait(false);
						cancellationToken.ThrowIfCancellationRequested();
						if (exitCode != 0)
							return false;
					}
			}
			return true;
		}

		/// <inheritdoc />
		public async Task<bool> DeleteDirectory(string configurationRelativePath, ISystemIdentity systemIdentity, CancellationToken cancellationToken)
		{
			await EnsureDirectories(cancellationToken).ConfigureAwait(false);
			var path = ValidateConfigRelativePath(configurationRelativePath);
			
			var result = false;
			using (await SemaphoreSlimContext.Lock(semaphore, cancellationToken).ConfigureAwait(false))
			{
				void CheckDeleteImpl() => result = synchronousIOManager.DeleteDirectory(path);

				if (systemIdentity != null)
					await systemIdentity.RunImpersonated(CheckDeleteImpl, cancellationToken).ConfigureAwait(false);
				else
					CheckDeleteImpl();
			}
			return result;
		}
	}
}
