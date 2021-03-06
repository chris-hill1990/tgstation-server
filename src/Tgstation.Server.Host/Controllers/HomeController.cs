﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Tgstation.Server.Api;
using Tgstation.Server.Host.Core;
using Tgstation.Server.Host.Models;
using Tgstation.Server.Host.Security;

namespace Tgstation.Server.Host.Controllers
{
	/// <summary>
	/// Main <see cref="ApiController"/> for the <see cref="Application"/>
	/// </summary>
	[Route(Routes.Root)]
	public sealed class HomeController : ApiController
	{
		/// <summary>
		/// The <see cref="ITokenFactory"/> for the <see cref="HomeController"/>
		/// </summary>
		readonly ITokenFactory tokenFactory;
		/// <summary>
		/// The <see cref="ISystemIdentityFactory"/> for the <see cref="HomeController"/>
		/// </summary>
		readonly ISystemIdentityFactory systemIdentityFactory;
		/// <summary>
		/// The <see cref="ICryptographySuite"/> for the <see cref="HomeController"/>
		/// </summary>
		readonly ICryptographySuite cryptographySuite;
		/// <summary>
		/// The <see cref="IApplication"/> for the <see cref="HomeController"/>
		/// </summary>
		readonly IApplication application;
		/// <summary>
		/// The <see cref="IIdentityCache"/> for the <see cref="HomeController"/>
		/// </summary>
		readonly IIdentityCache identityCache;

		/// <summary>
		/// Construct a <see cref="HomeController"/>
		/// </summary>
		/// <param name="databaseContext">The <see cref="IDatabaseContext"/> for the <see cref="ApiController"/></param>
		/// <param name="authenticationContextFactory">The <see cref="IAuthenticationContextFactory"/> for the <see cref="ApiController"/></param>
		/// <param name="tokenFactory">The value of <see cref="tokenFactory"/></param>
		/// <param name="systemIdentityFactory">The value of <see cref="systemIdentityFactory"/></param>
		/// <param name="cryptographySuite">The value of <see cref="cryptographySuite"/></param>
		/// <param name="application">The value of <see cref="application"/></param>
		/// <param name="identityCache">The value of <see cref="identityCache"/></param>
		/// <param name="logger">The <see cref="ILogger"/> for the <see cref="ApiController"/></param>
		public HomeController(IDatabaseContext databaseContext, IAuthenticationContextFactory authenticationContextFactory, ITokenFactory tokenFactory, ISystemIdentityFactory systemIdentityFactory, ICryptographySuite cryptographySuite, IApplication application, IIdentityCache identityCache, ILogger<HomeController> logger) : base(databaseContext, authenticationContextFactory, logger, false)
		{
			this.tokenFactory = tokenFactory ?? throw new ArgumentNullException(nameof(tokenFactory));
			this.systemIdentityFactory = systemIdentityFactory ?? throw new ArgumentNullException(nameof(systemIdentityFactory));
			this.cryptographySuite = cryptographySuite ?? throw new ArgumentNullException(nameof(cryptographySuite));
			this.application = application ?? throw new ArgumentNullException(nameof(application));
			this.identityCache = identityCache ?? throw new ArgumentNullException(nameof(identityCache));
		}

		/// <summary>
		/// Returns the version of the <see cref="Application"/>
		/// </summary>
		/// <returns><see cref="Application.Version"/></returns>
		[TgsAuthorize]
		[HttpGet]
		public JsonResult Home() => Json(new Api.Models.ServerInformation
		{
			Version = application.Version,
			ApiVersion = ApiHeaders.Version
		});

		/// <summary>
		/// Attempt to authenticate a <see cref="User"/> using <see cref="ApiController.ApiHeaders"/>
		/// </summary>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation</param>
		/// <returns>A <see cref="Task{TResult}"/> resulting in the <see cref="IActionResult"/> of the operation</returns>
		[HttpPost]
		public async Task<IActionResult> CreateToken(CancellationToken cancellationToken)
		{
			if (ApiHeaders.IsTokenAuthentication)
				return BadRequest(new Api.Models.ErrorMessage { Message = "Cannot create a token using another token!" });

			ISystemIdentity identity;
			try
			{
				//trust the system over the database because a user's name can change while still having the same SID
				identity = await systemIdentityFactory.CreateSystemIdentity(ApiHeaders.Username, ApiHeaders.Password, cancellationToken).ConfigureAwait(false);
			}
			catch (NotImplementedException)
			{
				identity = null;
			}
			using (identity)
			{
				IQueryable<User> query;
				if (identity == null)
					query = DatabaseContext.Users.Where(x => x.CanonicalName == ApiHeaders.Username.ToUpperInvariant());
				else
					query = DatabaseContext.Users.Where(x => x.SystemIdentifier == identity.Uid);
				var user = await query.Select(x => new User
				{
					Id = x.Id,
					PasswordHash = x.PasswordHash,
					Enabled = x.Enabled,
					Name = x.Name
				}).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

				if (user == null)
					return Unauthorized();

				if (identity == null)
				{
					var originalHash = user.PasswordHash;
					if (!cryptographySuite.CheckUserPassword(user, ApiHeaders.Password))
						return Unauthorized();
					if (user.PasswordHash != originalHash)
					{
						var updatedUser = new User
						{
							Id = user.Id
						};
						DatabaseContext.Users.Attach(updatedUser);
						updatedUser.PasswordHash = user.PasswordHash;
						await DatabaseContext.Save(cancellationToken).ConfigureAwait(false);
					}
				}
				//check if the name changed and updoot accordingly
				else if (identity.Username != user.Name)
				{
					DatabaseContext.Users.Attach(user);
					user.Name = identity.Username;
					user.CanonicalName = user.Name.ToUpperInvariant();
					await DatabaseContext.Save(cancellationToken).ConfigureAwait(false);
				}

				if (!user.Enabled.Value)
					return Forbid();

				var token = tokenFactory.CreateToken(user);
				if (identity != null)
					identityCache.CacheSystemIdentity(user, identity, token.ExpiresAt.Value.AddMinutes(1));   //expire the identity slightly after the auth token in case of lag

				Logger.LogDebug("Successfully logged in user {0}!", user.Id);

				return Json(token);
			}
		}
	}
}
