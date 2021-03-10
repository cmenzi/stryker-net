using Buildalyzer;
using FSharp.Compiler.SourceCodeServices;
using FSharp.Compiler.Text;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Stryker.Core.Exceptions;
using Stryker.Core.Options;
using Stryker.Core.ProjectComponents;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
using static FSharp.Compiler.SyntaxTree.ParsedInput;
using ParsedInput = FSharp.Compiler.SyntaxTree.ParsedInput;

namespace Stryker.Core.Initialisation
{
    internal class FsharpProjectComponentsBuilder : ProjectComponentsBuilder
    {
        private readonly ProjectInfo _projectInfo;
        private readonly IStrykerOptions _options;
        private readonly string[] _foldersToExclude;
        private readonly ILogger _logger;
        private readonly IFileSystem _fileSystem;

        public FsharpProjectComponentsBuilder(ProjectInfo projectInfo, IStrykerOptions options, string[] foldersToExclude, ILogger logger, IFileSystem fileSystem)
        {
            _projectInfo = projectInfo;
            _options = options;
            _foldersToExclude = foldersToExclude;
            _logger = logger;
            _fileSystem = fileSystem;
        }

        public override IProjectComponent Build()
        {
            FsharpFolderComposite inputFiles;
            if (_projectInfo.ProjectUnderTestAnalyzerResult.SourceFiles != null && _projectInfo.ProjectUnderTestAnalyzerResult.SourceFiles.Any())
            {
                inputFiles = FindProjectFilesUsingBuildalyzer(_projectInfo.ProjectUnderTestAnalyzerResult);
            }
            else
            {
                inputFiles = FindProjectFilesScanningProjectFolders(_projectInfo.ProjectUnderTestAnalyzerResult, _options);
            }
            return inputFiles;
        }
        private FsharpFolderComposite FindProjectFilesUsingBuildalyzer(IAnalyzerResult analyzerResult)
        {
            var inputFiles = new FsharpFolderComposite();
            var projectUnderTestDir = Path.GetDirectoryName(analyzerResult.ProjectFilePath);
            var projectRoot = Path.GetDirectoryName(projectUnderTestDir);
            var rootFolderComposite = new FsharpFolderComposite()
            {
                FullPath = projectRoot,
                RelativePath = string.Empty
            };
            var cache = new Dictionary<string, FsharpFolderComposite> { [string.Empty] = rootFolderComposite };

            // Save cache in a singleton so we can use it in other parts of the project
            FolderCompositeCache<FsharpFolderComposite>.Instance.Cache = cache;

            inputFiles.Add(rootFolderComposite);

            var fSharpChecker = FSharpChecker.Create(projectCacheSize: null, keepAssemblyContents: null, keepAllBackgroundResolutions: null, legacyReferenceResolver: null, tryGetMetadataSnapshot: null, suggestNamesForErrors: null, keepAllBackgroundSymbolUses: null, enableBackgroundItemKeyStoreAndSemanticClassification: null);

            foreach (var sourceFile in analyzerResult.SourceFiles)
            {
                // Skip xamarin UI generated files
                if (sourceFile.EndsWith(".xaml.cs"))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(projectUnderTestDir, sourceFile);
                var folderComposite = GetOrBuildFolderComposite(cache, Path.GetDirectoryName(relativePath), projectUnderTestDir, projectRoot, inputFiles);
                var fileName = Path.GetFileName(sourceFile);

                var file = new FsharpFileLeaf()
                {
                    SourceCode = _fileSystem.File.ReadAllText(sourceFile),
                    RelativePath = _fileSystem.Path.Combine(folderComposite.RelativePath, fileName),
                    FullPath = sourceFile
                };

                // Get the syntax tree for the source file
                Tuple<FSharpProjectOptions, FSharpList<FSharpErrorInfo>> fsharpoptions = FSharpAsync.RunSynchronously(fSharpChecker.GetProjectOptionsFromScript(filename: file.FullPath, sourceText: SourceText.ofString(file.SourceCode), previewEnabled: null, loadedTimeStamp: null, otherFlags: null, useFsiAuxLib: null, useSdkRefs: null, assumeDotNetFramework: null, extraProjectInfo: null, optionsStamp: null, userOpName: null), timeout: null, cancellationToken: null);
                FSharpParseFileResults result = FSharpAsync.RunSynchronously(fSharpChecker.ParseFile(fileName, SourceText.ofString(file.SourceCode), fSharpChecker.GetParsingOptionsFromProjectOptions(fsharpoptions.Item1).Item1, userOpName: null), timeout: null, cancellationToken: null);

                if (result.ParseTree.Value.IsImplFile)
                {
                    var syntaxTree = (ImplFile)result.ParseTree.Value;

                    file.SyntaxTree = syntaxTree;
                    folderComposite.Add(file);
                }
                else
                {
                    var message = $"Cannot make Fsharp SyntaxTree from .fsi filetype (SyntaxTree.ParsedImplFileInput class wanted)";
                    throw new StrykerInputException(message);
                }
            }
            return inputFiles;
        }

        // get the FolderComposite object representing the the project's folder 'targetFolder'. Build the needed FolderComposite(s) for a complete path
        private FsharpFolderComposite GetOrBuildFolderComposite(IDictionary<string, FsharpFolderComposite> cache, string targetFolder, string projectUnderTestDir,
            string projectRoot, ProjectComponent<ParsedInput> inputFiles)
        {
            if (cache.ContainsKey(targetFolder))
            {
                return cache[targetFolder];
            }

            var folder = targetFolder;
            FsharpFolderComposite subDir = null;
            while (!string.IsNullOrEmpty(folder))
            {
                if (!cache.ContainsKey(folder))
                {
                    // we have not scanned this folder yet
                    var sub = Path.GetFileName(folder);
                    var fullPath = _fileSystem.Path.Combine(projectUnderTestDir, sub);
                    var newComposite = new FsharpFolderComposite
                    {
                        FullPath = fullPath,
                        RelativePath = Path.GetRelativePath(projectRoot, fullPath),
                    };
                    if (subDir != null)
                    {
                        newComposite.Add(subDir);
                    }

                    cache.Add(folder, newComposite);
                    subDir = newComposite;
                    folder = Path.GetDirectoryName(folder);
                    if (string.IsNullOrEmpty(folder))
                    {
                        // we are at root
                        ((IFolderComposite)inputFiles).Add(subDir);
                    }
                }
                else
                {
                    (cache[folder]).Add(subDir);
                    break;
                }
            }

            return cache[targetFolder];
        }

        private FsharpFolderComposite FindProjectFilesScanningProjectFolders(IAnalyzerResult analyzerResult, IStrykerOptions options)
        {
            var inputFiles = new FsharpFolderComposite();
            var projectUnderTestDir = Path.GetDirectoryName(analyzerResult.ProjectFilePath);
            foreach (var dir in ExtractProjectFolders(analyzerResult, _fileSystem))
            {
                var folder = _fileSystem.Path.Combine(Path.GetDirectoryName(projectUnderTestDir), dir);

                _logger.LogDebug($"Scanning {folder}");
                if (!_fileSystem.Directory.Exists(folder))
                {
                    throw new DirectoryNotFoundException($"Can't find {folder}");
                }

                inputFiles.Add(FindInputFiles(projectUnderTestDir, analyzerResult));
            }

            return inputFiles;
        }

        /// <summary>
        /// Recursively scans the given directory for files to mutate
        /// </summary>
        private FsharpFolderComposite FindInputFiles(string path, IAnalyzerResult analyzerResult)
        {
            var rootFolderComposite = new FsharpFolderComposite
            {
                FullPath = Path.GetFullPath(path),
                RelativePath = Path.GetFileName(path),
            };

            rootFolderComposite.Add(
                FindInputFiles(path, Path.GetDirectoryName(analyzerResult.ProjectFilePath), rootFolderComposite.RelativePath)
            );
            return rootFolderComposite;
        }

        /// <summary>
        /// Recursively scans the given directory for files to mutate
        /// </summary>
        private FsharpFolderComposite FindInputFiles(string path, string projectUnderTestDir, string parentFolder)
        {
            var lastPathComponent = Path.GetFileName(path);

            var folderComposite = new FsharpFolderComposite
            {
                FullPath = Path.GetFullPath(path),
                RelativePath = Path.Combine(parentFolder, lastPathComponent),
            };

            foreach (var folder in _fileSystem.Directory.EnumerateDirectories(folderComposite.FullPath).Where(x => !_foldersToExclude.Contains(Path.GetFileName(x))))
            {
                folderComposite.Add(FindInputFiles(folder, projectUnderTestDir, folderComposite.RelativePath));
            }
            var fSharpChecker = FSharpChecker.Create(projectCacheSize: null, keepAssemblyContents: null, keepAllBackgroundResolutions: null, legacyReferenceResolver: null, tryGetMetadataSnapshot: null, suggestNamesForErrors: null, keepAllBackgroundSymbolUses: null, enableBackgroundItemKeyStoreAndSemanticClassification: null);
            foreach (var file in _fileSystem.Directory.GetFiles(folderComposite.FullPath, "*.fs", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);

                var fileLeaf = new FsharpFileLeaf()
                {
                    SourceCode = _fileSystem.File.ReadAllText(file),
                    RelativePath = Path.Combine(folderComposite.RelativePath, fileName),
                    FullPath = file,
                };

                // Get the syntax tree for the source file
                Tuple<FSharpProjectOptions, FSharpList<FSharpErrorInfo>> fsharpoptions = FSharpAsync.RunSynchronously(fSharpChecker.GetProjectOptionsFromScript(fileLeaf.FullPath, SourceText.ofString(fileLeaf.SourceCode), previewEnabled: null, loadedTimeStamp: null, otherFlags: null, useFsiAuxLib: null, useSdkRefs: null, assumeDotNetFramework: null, extraProjectInfo: null, optionsStamp: null, userOpName: null), timeout: null, cancellationToken: null);
                FSharpParseFileResults result = FSharpAsync.RunSynchronously(fSharpChecker.ParseFile(fileLeaf.FullPath, SourceText.ofString(fileLeaf.SourceCode), fSharpChecker.GetParsingOptionsFromProjectOptions(fsharpoptions.Item1).Item1, userOpName: null), timeout: null, cancellationToken: null);

                if (result.ParseTree.Value.IsImplFile)
                {
                    var syntaxTree = (ImplFile)result.ParseTree.Value;

                    fileLeaf.SyntaxTree = syntaxTree;

                    folderComposite.Add(fileLeaf);
                }
                else
                {
                    var message = $"Cannot make Fsharp SyntaxTree from .fsi filetype (SyntaxTree.ParsedImplFileInput class wanted)";
                    throw new StrykerInputException(message);
                }
            }

            return folderComposite;
        }
    }
}