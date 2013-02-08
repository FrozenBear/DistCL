﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Policy;
using DistCL.Client.CompileService;
using DistCL.Utils;

namespace DistCL.Client
{
    class Program
    {
        private static int Main(string[] args)
        {
            //return Compile("test arguments", ConfigurationManager.OpenExeConfiguration(typeof(Program).Assembly.Location).FilePath);
            return Compile("test arguments", @"D:\temp\deadlocks\5\poa.debug.log.dev07");
        }

        public static int Compile(string arguments, string srcFile)
        {
            ILocalCompiler compiler = new LocalCompilerClient("basicHttpEndpoint_LocalCompiler");

            CompileOutput output = compiler.LocalCompile(new LocalCompileInput {Arguments = arguments, Src = srcFile});

            var streams = new Dictionary<CompileArtifactType, Stream>();
            
            foreach (var artifact in output.Status.Cookies)
            {
                switch (artifact.Type)
                {
                    case CompileArtifactType.Out:
                        streams.Add(artifact.Type, Console.OpenStandardOutput());
                        break;

                    case CompileArtifactType.Err:
                        streams.Add(artifact.Type, Console.OpenStandardError());
                        break;

                    default:
                        throw new NotSupportedException("Not supported stream type");
                }
            }

            CompileResultHelper.Unpack(output.ResultData, output.Status.Cookies, streams);
            output.ResultData.Close();

            foreach (var stream in streams.Values)
            {
                stream.Close();
            }

            return output.Status.ExitCode;
        }
    }
}