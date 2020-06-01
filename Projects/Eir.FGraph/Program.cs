using System;
using System.Collections.Generic;
using System.IO;
using Hl7.Fhir.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FGraph
{
    class Program
    {
        class Options
        {
            public class Rendering
            {
                public String name { get; set; }
                public String cssFile { get; set; }
            };

            public String graphName { get; set; }
            public String inputPath { get; set; }
            public String outputDir { get; set; }
            public String baseUrl { get; set; }
            public String[] resourcePaths { get; set; }
            public Rendering[] traversals { get; set; }
            public String cssBinding { get; set; }
            public String cssFix { get; set; }
            public String cssPattern { get; set; }
        }

        FGrapher fGrapher;
        String inputDir = null;
        private Options options;

        public Program()
        {
            this.fGrapher = new FGrapher();
            this.fGrapher.ConsoleLogging();
        }


        //void CreateOptions(String path)
        //{
        //    Options o = new Options();
        //    o.graphName = "focus";
        //    o.inputPath = "GraphFiles";
        //    o.resourcePaths = new string[] { "build\\input\\profiles", "build\\input\\vocabulary" };
        //    o.traversals = new Rendering[]
        //    {
        //            new Rendering
        //            {
        //                name = "focus",
        //                cssFile = "FocusGraph.css"
        //            }
        //    };
        //    String j = JsonConvert.SerializeObject(o);
        //    File.WriteAllText(path, j);
        //}

        void ParseCommands(String path)
        {
            String fullPath = Path.GetFullPath(path);
            if (File.Exists(path) == false)
                throw new Exception($"Options file {path} not found");
            String json = File.ReadAllText(path);
            this.options = JsonConvert.DeserializeObject<Options>(json);
        }

        void ParseArguments(String[] args)
        {
            switch (args.Length)
            {
                case 0:
                    ParseCommands("fgraph.json");
                    break;
                case 1:
                    ParseCommands(args[0]);
                    break;
                default:
                    throw new Exception($"Unexpected parameters");
            }
        }

        bool Process()
        {
            const String fcn = "Process";

            if (String.IsNullOrEmpty(options.outputDir))
                throw new Exception("Missing 'outputDir' option setting");
            this.fGrapher.OutputDir = options.outputDir;

            if (String.IsNullOrEmpty(options.graphName))
                throw new Exception("Missing 'graphName' option setting");
            this.fGrapher.GraphName = options.graphName;

            if (String.IsNullOrEmpty(options.cssBinding) == false)
                this.fGrapher.BindingNode_CssClass = options.cssBinding;

            if (String.IsNullOrEmpty(options.cssFix) == false)
                this.fGrapher.FixNode_CssClass = options.cssFix;

            if (String.IsNullOrEmpty(options.cssPattern) == false)
                this.fGrapher.PatternNode_CssClass = options.cssPattern;

            if (String.IsNullOrEmpty(options.baseUrl))
                throw new Exception("Missing 'baseUrl' option setting");
            this.fGrapher.BaseUrl = options.baseUrl;
            if (this.fGrapher.BaseUrl.EndsWith("/") == false)
                this.fGrapher.BaseUrl += "/";

            this.fGrapher.ConversionInfo(fcn, $"Loading");
            foreach (String resourcePath in options.resourcePaths)
                this.fGrapher.LoadResources(resourcePath);

            if (String.IsNullOrEmpty(options.inputPath))
                throw new Exception("Missing 'inputPath' option setting");
            this.fGrapher.Load(options.inputPath);
            if (Directory.Exists(options.inputPath))
                this.inputDir = options.inputPath;
            else
                this.inputDir = Path.GetDirectoryName(options.inputPath);

            this.fGrapher.ConversionInfo(fcn, $"Processing");
            this.fGrapher.Process();
            foreach (Options.Rendering rendering in this.options.traversals)
            {
                bool Exists(String dir, ref String relativePath)
                {
                    String checkPath = Path.Combine(dir, relativePath);
                    if (File.Exists(checkPath) == false)
                        return false;
                    relativePath = checkPath;
                    return true;
                }

                String cssFile = rendering.cssFile;
                if (
                    (!Exists(Path.GetFullPath("."), ref cssFile)) &&
                    (!Exists(this.inputDir, ref cssFile))
                )
                    throw new Exception($"Css file '{cssFile}' not found");

                switch (rendering.name.ToLower())
                {
                    case "focus":
                        this.fGrapher.RenderFocusGraphs(cssFile);
                        break;
                    default:
                        throw new NotImplementedException($"Rendering '{rendering.name}' is not known");
                }
            }

            this.fGrapher.ConversionInfo(fcn, $"Saving");
            this.fGrapher.SaveAll();
            this.fGrapher.ConversionInfo(fcn, $"Done");
            return this.fGrapher.HasErrors == false;
        }

        static Int32 Main(string[] args)
        {
            try
            {
                Program p = new Program();
                p.ParseArguments(args);
                if (p.Process() == false)
                    return -1;
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return -1;
            }
        }
    }
}
