﻿using Moq;
using TGS.Interface;
using TGS.Interface.Components;
using TGServiceTests;

namespace TGS.CommandLine.Commands.Repository.Tests
{
	public abstract class RepositoryCommandTest : OutputProcOverriderTest
	{
		/// <summary>
		/// Creates a mock <see cref="IServerInterface"/> that returns a specific <see cref="ITGRepository"/> when that component is requested
		/// </summary>
		/// <param name="repo">The <see cref="ITGRepository"/> the resulting <see cref="IServerInterface.GetComponent{T}"/> should return</param>
		/// <returns>A mock <see cref="IServerInterface"/></returns>
		protected IInstance MockInterfaceToRepo(ITGRepository repo)
		{
			var mock = new Mock<IInstance>();
			mock.Setup(foo => foo.Repository).Returns(repo);
			return mock.Object;
		}
	}
}
