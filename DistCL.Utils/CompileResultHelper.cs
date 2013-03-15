using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization;
using DistCL.Utils.Streams;

namespace DistCL.Utils
{
	[DataContract(Namespace = CompileResultHelper.Namespace)]
	public enum CompileArtifactType
	{
		[EnumMember]
		Obj,
		[EnumMember]
		Pdb,
		[EnumMember]
		Out,
		[EnumMember]
		Err,
		[EnumMember]
		Src
	}

	public interface ICompileArtifactDescription
	{
		CompileArtifactType Type { get; }
	}

	public interface ICompileArtifactCookie : ICompileArtifactDescription
	{
		long Size { get; }
	}

	[DataContract(Namespace = CompileResultHelper.Namespace)]
	public class CompileArtifactDescription : ICompileArtifactDescription
	{
		public CompileArtifactDescription()
		{
		}

		public CompileArtifactDescription(CompileArtifactType type, string name)
		{
			Type = type;
			Name = name;
		}

		[DataMember]
		public CompileArtifactType Type { get; set; }

		[DataMember]
		public string Name { get; set; }

		public override bool Equals(object obj)
		{
			var other = obj as CompileArtifactDescription;

			return other != null && (Type == other.Type && Name == other.Name);
		}

		public override int GetHashCode()
		{
			return Type.GetHashCode() ^ (Name == null ? 0 : Name.GetHashCode());
		}
	}

	[DataContract(Namespace = CompileResultHelper.Namespace)]
	public class CompileArtifactCookie : CompileArtifactDescription, ICompileArtifactCookie
	{
		public CompileArtifactCookie(CompileArtifactDescription description, long size)
		{
			Contract.Requires(description != null);
			Contract.Requires(size >= 0);

			Name = description.Name;
			Type = description.Type;
			Size = size;
		}

		[DataMember]
		public long Size { get; private set; }
	}

	public static class CompileResultHelper
	{
		internal const string Namespace = "urn:distcl:utils";

		public static Stream Pack(IDictionary<CompileArtifactDescription, Stream> streams, out CompileArtifactCookie[] cookies)
		{
			Contract.Requires(streams != null);

			var cookiesList = new List<CompileArtifactCookie>();
			var streamsList = new List<Stream>();

			foreach (var stream in streams)
			{
				cookiesList.Add(new CompileArtifactCookie(stream.Key, stream.Value.Length));
				streamsList.Add(stream.Value);
			}

			cookies = cookiesList.ToArray();
			return new MultiStream(streamsList);
		}

		public static void Unpack(Stream multiStream, IEnumerable<ICompileArtifactCookie> cookies, IDictionary<CompileArtifactType, Stream> streams)
		{
			Contract.Requires(multiStream != null);
			Contract.Requires(cookies != null);

			var splitter = new StreamSplitter(multiStream);

			foreach (var cookie in cookies)
			{
				if (cookie.Size < 0)
					continue;

				using (var stream = splitter.GetStream(cookie.Size))
				{
					stream.CopyTo(streams[cookie.Type]);
				}
			}
		}
	}
}