using System;
using DistCL.Utils;

namespace DistCL.Client.CompileService
{
	partial class CompileArtifactCookie : ICompileArtifactCookie
	{
		Utils.CompileArtifactType ICompileArtifactDescription.Type
		{
			get
			{
				switch (Type)
				{
						case CompileArtifactType.Obj:
						return Utils.CompileArtifactType.Obj;

						case CompileArtifactType.Pdb:
						return Utils.CompileArtifactType.Pdb;

						case CompileArtifactType.Out:
						return Utils.CompileArtifactType.Out;

						case CompileArtifactType.Err:
						return Utils.CompileArtifactType.Err;

						default: throw new NotSupportedException(Type.ToString());
				}
			}
		}
	}
}
