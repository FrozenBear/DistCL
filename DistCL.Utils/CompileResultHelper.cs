using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using DistCL.Utils.Streams;

namespace DistCL.Utils
{
    public enum CompileArtifactType
    {
        Obj,
        Pdb,
        Out,
        Err
    }

    [DataContract]
    public class CompileArtifactDescription
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
    [DataContract]
    public class CompileArtifactCookie : CompileArtifactDescription
    {
        public CompileArtifactCookie(CompileArtifactDescription description, long size)
        {
            Name = description.Name;
            Type = description.Type;
            Size = size;
        }

        [DataMember]
        public long Size { get; private set; }
    }

    public static class CompileResultHelper
    {
        public static Stream Pack(IDictionary<CompileArtifactDescription, Stream> streams, out CompileArtifactCookie[] cookies)
        {
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

        public static void Unpack(Stream multiStream, CompileArtifactCookie[] cookies, IDictionary<CompileArtifactType, Stream> streams)
        {
            var splitter = new StreamSplitter(multiStream);

            foreach (var cookie in cookies)
            {
                using (var stream = splitter.GetStream(cookie.Size))
                {
                    stream.CopyTo(streams[cookie.Type]);
                }
            }
        }
    }
}
