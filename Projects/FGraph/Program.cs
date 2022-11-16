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
            public class Traversal
            {
                public String name { get; set; } = String.Empty;
                public String param1 { get; set; } = String.Empty;
                public String param2 { get; set; } = String.Empty;
                public String keys { get; set; } = String.Empty;
                public Int32 depth { get; set; } = 99999;
                public String cssFile { get; set; } = String.Empty;
            };

            public String graphName { get; set; } = String.Empty;
            public String inputPath { get; set; } = String.Empty;
            public String outputDir { get; set; } = String.Empty;
            public String baseUrl { get; set; } = String.Empty;
            public String[] resourcePaths { get; set; } = new String[0];
            public Traversal[] traversals { get; set; } = new Traversal[0];
            public String cssBinding { get; set; } = String.Empty;
            public String cssFix { get; set; } = String.Empty;
            public String cssPattern { get; set; } = String.Empty;
        }

        FGrapher fGrapher;
        String inputDir = String.Empty;
        private Options? options;

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

            if (options == null)
                throw new Exception("Missing options");

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
                this.fGrapher.LoadResourcesStart(resourcePath);
            this.fGrapher.LoadResourcesWaitComplete();

            if (String.IsNullOrEmpty(options.inputPath))
                throw new Exception("Missing 'inputPath' option setting");
            this.fGrapher.Load(options.inputPath);
            if (Directory.Exists(options.inputPath))
                this.inputDir = options.inputPath;
            else
            {
                String? s = Path.GetDirectoryName(options.inputPath);
                this.inputDir = s ?? throw new Exception($"Invalid input path '{options.inputPath}'");
            }

            this.fGrapher.ConversionInfo(fcn, $"Processing");
            this.fGrapher.Process();

            foreach (Options.Traversal traversal in this.options.traversals)
            {
                bool Exists(String dir, ref String relativePath)
                {
                    String checkPath = Path.Combine(dir, relativePath);
                    if (File.Exists(checkPath) == false)
                        return false;
                    relativePath = checkPath;
                    return true;
                }

                Int32 depth = traversal.depth;
                String cssFile = traversal.cssFile;
                if (
                    (!Exists(Path.GetFullPath("."), ref cssFile)) &&
                    (!Exists(this.inputDir, ref cssFile))
                )
                {
                    throw new Exception($"Css file '{cssFile}' not found in '{Path.GetFullPath(".")}' or '{Path.GetFullPath(this.inputDir)}'");
                }

                switch (traversal.name.ToLower())
                {
                    case "frag":
                    case "focus":
                        this.fGrapher.RenderFocusGraphs(cssFile, traversal.name.ToLower(), depth);
                        break;
 
                    case "single":
                        if (traversal.keys == null)
                            throw new Exception($"Rendering.keys must be set");
                        if (String.IsNullOrEmpty(traversal.param1))
                            throw new Exception($"Rendering.param1 must be start node");
                        if (String.IsNullOrEmpty(traversal.param2))
                            throw new Exception($"Rendering.param2 must graph name");
                        this.fGrapher.RenderSingleNode(cssFile,
                            traversal.param1,
                            depth,
                            "focus",
                            traversal.param2,
                            traversal.keys);
                        break;
                    
                    default:
                        throw new NotImplementedException($"Rendering '{traversal.name}' is not known");
                }
            }

            this.fGrapher.ConversionInfo(fcn, $"Saving");
            this.fGrapher.SaveAll();
            this.fGrapher.ConversionInfo(fcn, $"Done");
            //this.fGrapher.DumpNodeLinks(@"c:\Temp\Dump.txt");
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
