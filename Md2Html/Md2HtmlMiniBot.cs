using System.Collections.Generic;
using System.Text.RegularExpressions;
using CodegenBot;
using HtmlAgilityPack;
using Markdig;

namespace Md2Html;

public class Md2HtmlMiniBot : IMiniBot
{
    public void Execute()
    {
        var configuration = GraphQLClient.GetConfiguration().Configuration;
        GetFileContents(configuration.FilesWhitelist, configuration.FilesBlacklist, out var markdownFiles);

        foreach (var markdownFile in markdownFiles)
        {
            var html = ToHtml(markdownFile.Content, configuration);
            
            var htmlFilePath = markdownFile.Path;

            if (configuration.OutputPathTransformers is not null && configuration.OutputPathTransformers.Count > 0)
            {
                foreach (var transformer in configuration.OutputPathTransformers)
                {
                    htmlFilePath = Regex.Replace(htmlFilePath, transformer.Regex, transformer.Replacement);
                }
            }
            else
            {
                if (htmlFilePath.EndsWith(".md"))
                {
                    htmlFilePath = htmlFilePath.Substring(0, htmlFilePath.Length - ".md".Length);
                }
                else if (htmlFilePath.EndsWith(".mdx"))
                {
                    htmlFilePath = htmlFilePath.Substring(0, htmlFilePath.Length - ".mdx".Length);
                }

                htmlFilePath = $"{htmlFilePath}.html";
            }
            
            Imports.Log(new LogEvent()
            {
                Level = LogEventLevel.Information,
                Message = "Converting {MarkdownFilePath} to {HtmlFilePath}",
                Args = [markdownFile.Path, htmlFilePath]
            });
            GraphQLClient.AddFile(htmlFilePath, html);
        }
    }
    
    public string ToHtml(string markdown, GetConfigurationConfiguration configuration)
    {
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdown.ToHtml(markdown, pipeline);

        // Process the generated HTML to add TailwindCSS classes
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        foreach (var rule in configuration.Css ?? [])
        {
            var elements = htmlDoc.DocumentNode.SelectNodes($"//{rule.Element}");
            if (elements != null)
            {
                foreach (var element in elements)
                {
                    element.SetAttributeValue("class", rule.Class);
                }
            }
        }

        html = htmlDoc.DocumentNode.OuterHtml;
        return html;
    }
    
    private static bool GetFileContents(List<string> whitelist, List<string> blacklist, out List<FileContents> currentFiles)
    {
        var files = GraphQLClient.GetFiles(whitelist, blacklist);

        currentFiles = new List<FileContents>();
        
        foreach (var file in files.Files ?? [])
        {
            var fileContents = GraphQLClient.ReadTextFile(file.Path).ReadTextFile;

            if (fileContents is null)
            {
                Imports.Log(new LogEvent()
                {
                    Level = LogEventLevel.Error,
                    Message = "Could not read file {Path} even though it should exist because it matched the whitelist.",
                    Args = [file.Path]
                });
                continue;
            }
            
            currentFiles.Add(new (file.Path, fileContents));
        }

        if (currentFiles.Count == 0)
        {
            Imports.Log(new LogEvent()
            {
                Level = LogEventLevel.Information,
                Message = "No files found. Exiting.",
                Args = []
            });
            return false;
        }

        return true;
    }
    
    public record FileContents(string Path, string Content);
}