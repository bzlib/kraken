using System;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Threading;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

//using Mono;
//using FirebirdSql.Data.FirebirdClient;
//using Mono.Data.Sqlite;
//using MongoDB.Driver;
//using MongoDB.Bson;
using System.Configuration;

using Kraken.Util;
using Kraken.Core;
using Kraken.Http;
using Http;
using WebDav;

namespace Kraken.CommandLine
{
    /// <summary>
    /// Main class.
    /// 
    /// The command line version of kraken.
    /// 
    /// The following are the available kraken commands.
    /// 
    /// kraken init <root_name:required>
    /// ================================
    /// 
    /// This will start treating the folder hierarchy as a repo to be managed (i.e. the files saved here can be stored 
    /// into the hierarchy).
    /// 
    /// The first first time, a .kraken/ repo will be created under your home directory (future might be configurable).
    /// 
    /// The root_name argument gives you a top-level identifier that you can use to identify the particular file.
    /// 
    /// The following example clarifies a bit more of the usage
    /// 
    /// $ cd ~/docs/hr
    /// $ kraken init hr # now ~/docs/hr is mapped to hr
    /// $ kraken save # all files within ~/docs/hr will now be committed into the repo
    /// $ cd # back to home directory
    /// $ kraken ls hr # list the files mapped under hr
    /// $ kraken restore hr ~/temp/hr # create a new copy of hr stored in kraken
    /// 
    /// kraken save
    /// ===========
    /// 
    /// This will pull the tree and push any differences into the kraken repo.
    /// 
    /// Note - the kraken repo differs by default from repos like git, that files are managed independently
    /// (i.e. things are not snapshotted together).
    /// 
    /// </summary>
	class MainClass
	{
        string rootPath;
        IniFile iniFile; 
        PathStore pathStore;
        HttpServer server;
        MimeTypeRegistry mimeTypes = new MimeTypeRegistry();
        protected MainClass() {
            ensureKrakenBase();
        }
        public static void Main(string[] args)
        {
            MainClass app = new MainClass();
            if (args.Length == 0)
            {
                app.showUsage();
            } else
            {

                switch (args [0])
                {
                    case "init":
                        app.initRepo(args);
                        break;
                    case "save":
                        app.savePath(args);
                        break;
                    case "restore":
                        app.restorePath(args);
                        break;
                    case "ls":
                        app.listPaths(args);
                        break;
                    case "list":
                        app.listPaths(args);
                        break;
                    case "li":
                        app.listPaths(args);
                        break;
                    case "raw":
                        app.getPath(args);
                        break;
                    case "checksum":
                        app.checksum(args);
                        break;
                    case "http":
                        app.callHttp(args);
                        break;
                    default:
                        app.unknownCommand(args [0]);
                        break;
                }
            }
        }

        void initRepo(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("krakan init <root_name> --> required");
                return;
            }
            // okay - we are here now - we should start to track a list of mappings of the name against 
            // the current path is...
            string rootName = args[1];
            string dirPath = Directory.GetCurrentDirectory();
            pathStore.RootPathMap.Add(rootName, dirPath);
        }

        void savePath(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("krakan save <from_local_path> <to_kraken_path> --> required");
                return;
            }
            string fromPath = args [1];
            string toPath = args [2];
            if (File.Exists(fromPath))
            {
                if (pathStore.IsDirectory(toPath)) {
                    // let's append the fileName to the end of the toPath.
                    pathStore.SaveOnePath(fromPath, FileUtil.ChangePathDirectory(fromPath, toPath));
                } else {
                    pathStore.SaveOnePath(fromPath, toPath);
                }
            } else if (Directory.Exists(fromPath))
            {
                if (pathStore.IsBlob(toPath)) {
                    Console.WriteLine("Cannot save a folder into a file: {0} is a folder, and {1} is a file", fromPath, toPath);
                    return;
                } else if (pathStore.IsDirectory(toPath)) {
                    Console.Write("Folder {0} exists - do you want to [m]erge or [r]eplace? [m/r] ", toPath);
                    string answer = Console.ReadLine().Trim().ToLower();
                    if (answer == "m") {
                        pathStore.SaveFolder(fromPath, toPath);
                    } else if (answer == "r") {
                        Console.WriteLine("Replace is currently unsupported yet (soon!)");
                    } else {
                        Console.WriteLine("Unknown response.");
                        return;
                    }
                } else {
                    pathStore.SaveFolder(fromPath, toPath);
                }
            } else
            {
                Console.WriteLine("kraken save: path does not exist {0}", fromPath);
            }
        }

        void restorePath(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("kraken restore <from_kraken_path> <to_local_path> --> required");
                return;
            }
            string fromPath = args [1];
            string toPath = args [2];
            // for now let's handle the single file case...
            // also - how do we know whether or not it's a path or a directory?
            // we also should remember the rules of the target.
            if (pathStore.IsBlob(fromPath))
            {
                if (Directory.Exists(toPath))
                {
                    pathStore.RestoreOnePath(fromPath, FileUtil.ChangePathDirectory(fromPath, toPath));
                    return;
                } else if (File.Exists(toPath))
                {
                    // this means we are overwriting the file.
                    Console.Write("File {0} exists - do you want to overwrite? [y/n] ", toPath);
                    string answer = Console.ReadLine().Trim().ToLower();
                    if (answer == "y")
                    {
                        pathStore.RestoreOnePath(fromPath, toPath);
                    } else
                    {
                        return;
                    }
                } else
                {
                    pathStore.RestoreOnePath(fromPath, toPath);
                }
            } else if (pathStore.IsDirectory(fromPath))
            {
                // now we will need to restore path.
                if (File.Exists(toPath)) {
                    Console.WriteLine("Cannot restore a directory into a file: {0} is a directory, and {1} is a file", fromPath, toPath);
                } else if (Directory.Exists(toPath)) {

                } else { // the simplest mode...
                    pathStore.RestoreFolder(fromPath, toPath);
                }
            } else
            { // neither 
                Console.WriteLine("Path {0} is not in the repo", fromPath);
            }
        }

        void listPaths(string[] args)
        {
            string listPath = ".";
            int depth = 0;
            if (args.Length > 1)
            {
                listPath = args [1];
                if (args.Length > 2) {
                    if (args[2].ToLower() == "infinity") {
                        depth = int.MaxValue;
                    } else {
                        depth = int.Parse(args[2]);
                    }
                }
            }
            foreach (string path in pathStore.ListPaths(listPath, depth))
            {
                Console.WriteLine("  {0}", path);
            }
        }

        void getPath(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("kraken raw <kraken_repo_path> --> required");
                return;
            }
            // once we have the path.
            string path = args [1];
            if (pathStore.IsBlob(path))
            {
                BlobStream blob = pathStore.GetBlob(path);
                blob.CopyTo(Console.OpenStandardOutput());
                return;
            } else if (pathStore.IsDirectory(path)) {
                Console.WriteLine("{0} is a directory", path);
                return;
            } else
            {
                Console.WriteLine("{0} is not in the repo", path);
                return;
            }

        }

        void checksum(string[] args)
        {
            if (args.Length == 1)
            {
                Console.WriteLine("kraken checksum <local_path> --> required");
                return;
            }
        }

        void callHttp(string[] args)
        {
            // ***THIS IS EXPERIMENTAL RIGHT NOW*** i.e. not hooked up.
            // we'll need to "block" the call.
            if (args.Length < 2)
            {
                Console.WriteLine("kraken http <start|stop> --> required");
                return;
            }
            if (server == null)
            {
                server = new HttpServer("http://*:8080/");
                server.AddRoute("get", "/path...", this.httpGetPath);
                server.AddRoute("put", "/path...", this.httpPutPath2);
                server.AddRoute("delete", "/path...", this.httpDeletePath);
                server.AddRoute("propfind", "/path...", this.httpPropfindPath);
            }
            server.Start();
            Console.WriteLine("kraken http is being developed - this is experimental");
            Console.WriteLine("Server Listening - Press any key to stop...");
            Console.ReadKey();
        }

        void httpGetPath(HttpContext context)
        {

            var path = context.UrlParams ["path"];
            Console.WriteLine("GET: {0}", path);
            if (pathStore.IsBlob(path))
            {
                using (BlobStream blob = pathStore.GetBlob(path)) {
                    if (context.Request.Headers["If-None-Match"] == blob.Envelope.Checksum) {
                        context.Response.StatusCode = 304;
                        context.Response.Headers["ETag"] = blob.Envelope.Checksum;
                        context.Response.Headers["Server"] = "Kraken/0.1";
                        string mimeType = mimeTypes.PathToMimeType(path);
                        if (!string.IsNullOrEmpty(mimeType)) 
                            context.Response.ContentType = mimeType;
                        Console.WriteLine("ContentType: {0} -> {1} => {2}", path, mimeType, context.Response.ContentType);
                        context.Response.SetOutput("");
                        blob.Close();
                        Console.WriteLine("got here");
                    } else {
                        context.Response.StatusCode = 200;
                        context.Response.Headers["ETag"] = blob.Envelope.Checksum;
                        context.Response.Headers["Server"] = "Kraken/0.1";
                        string mimeType = mimeTypes.PathToMimeType(path);
                        if (!string.IsNullOrEmpty(mimeType)) 
                            context.Response.ContentType = mimeType;
                        Console.WriteLine("ContentType: {0} -> {1} => {2}", path, mimeType, context.Response.ContentType);
                        context.Response.SetOutput(blob);
                    }
                }
            } else
            {
                throw new HttpException(404);
            }
        }

        void httpPutPath2(HttpContext context)
        {
            string path = context.UrlParams ["path"];
            // let's convert the path into something that can be viewed.
            Regex slashRE = new Regex(@"\/");
            string normalized = path.Replace("/", "_");
            Directory.CreateDirectory(System.IO.Path.Combine(rootPath, "uploads"));
            string tempFilePath = FileUtil.TempFilePath(System.IO.Path.Combine(rootPath, "uploads", normalized));
            using (FileStream fs = File.Open(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                context.Request.InputStream.CopyTo(fs);
                // it does look like that we should handle the input stream.
                // also it looks like chunked is automatically processed by HttpListenerRequest
            }
            context.Response.Respond(201);
        }

        void httpPutPath(HttpContext context)
        {
            // in this particular case we'll put up one file...
            // and we'll store the file into the location pinpoint at the place.
            // if successful return 201 (with ETag set).
            string path = context.UrlParams ["path"];
            try
            {
                pathStore.SaveStream(context.Request.InputStream, path);
                context.Response.Respond(201);
            } catch (Exception e)
            {
                Console.WriteLine("PUT: {0} ERROR: {1}", path, e);
                throw new HttpException(500, "PUT FAILED: {0}", e);
            }
        }

        void httpDeletePath(HttpContext context)
        {
            string path = context.UrlParams ["path"];
            if (pathStore.IsDirectory(path))
            {
                pathStore.DeleteFolder(path);
                context.Response.Respond(204);
            } else if (pathStore.IsBlob(path))
            {
                pathStore.DeletePath(path);
                context.Response.Respond(204);
            } else
            { // doesn't exist - it's a NO OP.
                context.Response.Respond(204);
            }
        }

        void httpPropfindPath(HttpContext context)
        {
            string path = context.UrlParams ["path"];
            // we need to be able to parse the XML for manipulation...
            // if there are no XML - do we consider this a BAD request? 
            if (context.Request.ContentType != "application/xml")
            {
                throw new HttpException(400, "payload_not_xml");
            } 

            WebDav.Factory factory = new Factory();

            WebDav.Request req = factory.ParseRequest(context.Request);
            // propname -> return a list of available propnames.
            // we can now parse the reqest -> we'll need to further determine whether or not we are looking for 
            // a particular depth.

            string resp = File.ReadAllText("testpropfind.xml", Encoding.UTF8);

            context.Response.ContentType = "application/xml";
            context.Response.Respond(207, resp);
        }

        void httpMakeCollection(HttpContext context)
        {
            string path = context.UrlParams["path"];
        }

        void ensureKrakenBase()
        {
            rootPath = System.IO.Path.Combine(FileUtil.GetHomeDirectory(), ".kraken");
            string iniPath = System.IO.Path.Combine(rootPath, "kraken.ini");
            if (File.Exists(iniPath))
            {
                iniFile = new IniFile(iniPath);
                pathStore = new PathStore(iniFile.GetSection("base"));
            } else
            {
                iniFile = new IniFile("./kraken.ini");
                if (!iniFile.Contains("base", "rootPath"))
                    iniFile.Add("base", "rootPath", rootPath);
                pathStore = new PathStore(iniFile.GetSection("base")); // how do we ensure that things are under the home directory?
                iniFile.Save(iniPath); // this is for managing multiple repos... at the end it simply maps to a single path
                // directory... but things won't start right underneath the pathRoot.
                // when we do a save - we wouldn't know what has changed by default... do we?
                // i.e. how can we do this part fast? 
            }
        }

        void unknownCommand(string command) {
            Console.WriteLine("Unknown command: {0}", command);
            showUsage();
        }

        void showUsage() {
            Console.WriteLine("Usage: kraken <command> <args>");
        }

    }
}
