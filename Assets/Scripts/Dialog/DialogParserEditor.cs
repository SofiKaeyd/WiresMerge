#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using TMPro;
using UnityEditor;
using UnityEngine.TextCore;
using UnityEngine;

public class DialogGraphWindow : EditorWindow
{
    private const string GeneratedAssetPrefix = "DialogGraphInlineImage_";

    private DialogGraph _graph;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Dialog Graph Manager")]
    public static void ShowWindow()
    {
        GetWindow<DialogGraphWindow>("Dialog Graph Manager");
    }

    private void OnEnable()
    {
        RefreshGraphReference();
    }

    private void RefreshGraphReference()
    {
        var guids = AssetDatabase.FindAssets("t:DialogGraph");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _graph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path);
        }
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        DrawHeader();
        DrawGraphSelection();

        if (_graph != null)
        {
            DrawControls();
            DrawStatistics();
        }
        else
        {
            DrawNoGraphWarning();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dialog Graph Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();
    }

    private void DrawGraphSelection()
    {
        EditorGUILayout.BeginHorizontal();
        {
            _graph = (DialogGraph)EditorGUILayout.ObjectField("Dialog Graph", _graph, typeof(DialogGraph), false);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshGraphReference();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawControls()
    {
        EditorGUILayout.HelpBox(
            "Append SourceImageUrl and RecipientImageUrl after IsCorrect. Use publicly accessible Google Drive links. " +
            "In Text, {0} inserts the source image and {1} the recipient image; in rows with image links, " +
            "text Parameters then start at {2}. Images are downloaded during import and stored in the graph for offline play.",
            MessageType.Info);
        EditorGUILayout.Space();
        if (GUILayout.Button("Load from Google Sheets", GUILayout.Height(30)))
        {
            LoadFromGoogleSheets();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Clear Data", GUILayout.Height(25)))
        {
            ClearData();
        }

        EditorGUILayout.Space(15);
    }

    private void DrawStatistics()
    {
        EditorGUILayout.LabelField("Statistics:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Loaded Nodes: {_graph.Nodes.Count}");
    }

    private void DrawNoGraphWarning()
    {
        EditorGUILayout.HelpBox("No Dialog Graph asset found in project. Please create or assign one.", MessageType.Warning);

        EditorGUILayout.Space();
        if (GUILayout.Button("Create New Dialog Graph"))
        {
            CreateNewDialogGraph();
        }
    }

    private void LoadFromGoogleSheets()
    {
        var success = DialogParser.LoadAndParseFromGoogleSheets();
        if (!success)
        {
            EditorUtility.DisplayDialog("Error", "Failed to load dialog data from Google Sheets", "OK");
            return;
        }

        var bundles = new List<InlineImageAssetBundle>();
        try
        {
            bundles = CreateInlineImageAssets(DialogParser.DialogGraph);
            AddInlineImageAssetsToGraph(bundles);

            foreach (var bundle in bundles)
                bundle.Node.InlineImageAsset = bundle.SpriteAsset;

            _graph.Set(DialogParser.DialogGraph);
            RemoveGeneratedAssetsNotIn(bundles);
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", $"Successfully loaded {_graph.Nodes.Count} dialog nodes", "OK");
            Repaint();
        }
        catch (Exception ex)
        {
            DestroyUnattachedBundles(bundles);
            Debug.LogError($"Failed to import dialog images: {ex}");
            EditorUtility.DisplayDialog("Error", $"Failed to import dialog images:\n{ex.Message}", "OK");
        }
    }

    private void ClearData()
    {
        if (EditorUtility.DisplayDialog("Clear Data",
            "Are you sure you want to clear all dialog data?", "Yes", "No"))
        {
            RemoveGeneratedAssetsNotIn(new List<InlineImageAssetBundle>());
            _graph.ClearData();
            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            Repaint();
        }
    }

    private List<InlineImageAssetBundle> CreateInlineImageAssets(DialogGraph graph)
    {
        var bundles = new List<InlineImageAssetBundle>();
        try
        {
            var nodesWithImages = graph.Nodes.FindAll(node => node.HasInlineImages);
            for (var i = 0; i < nodesWithImages.Count; i++)
            {
                var node = nodesWithImages[i];
                EditorUtility.DisplayProgressBar(
                    "Importing dialog images",
                    $"Downloading images for node {node.Id}",
                    (i + 1f) / nodesWithImages.Count);
                bundles.Add(CreateInlineImageAsset(node));
            }

            return bundles;
        }
        catch
        {
            DestroyUnattachedBundles(bundles);
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private InlineImageAssetBundle CreateInlineImageAsset(DialogNode node)
    {
        var images = new List<InlineImage>();
        if (!string.IsNullOrWhiteSpace(node.SourceImageUrl))
            images.Add(new InlineImage(DialogNode.SourceImageSpriteName, DownloadImage(node.SourceImageUrl)));
        if (!string.IsNullOrWhiteSpace(node.RecipientImageUrl))
            images.Add(new InlineImage(DialogNode.RecipientImageSpriteName, DownloadImage(node.RecipientImageUrl)));

        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = GeneratedAssetPrefix + node.Id + "_Atlas",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Material material = null;
        TMP_SpriteAsset spriteAsset = null;
        try
        {
            var rects = atlas.PackTextures(
                images.ConvertAll(image => image.Texture).ToArray(),
                2,
                2048,
                false);
            if (rects.Length != images.Count)
                throw new InvalidDataException($"Could not pack images for dialog node {node.Id}.");

            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
                throw new InvalidOperationException("TextMeshPro/Sprite shader was not found.");

            material = new Material(shader)
            {
                name = GeneratedAssetPrefix + node.Id + "_Material",
                mainTexture = atlas
            };

            var template = TMP_Settings.defaultSpriteAsset;
            if (template == null)
                throw new InvalidOperationException("TextMeshPro default sprite asset was not found.");

            spriteAsset = Instantiate(template);
            spriteAsset.name = GeneratedAssetPrefix + node.Id + "_SpriteAsset";
            spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(spriteAsset.name);
            spriteAsset.spriteSheet = atlas;
            spriteAsset.material = material;
            spriteAsset.materialHashCode = TMP_TextUtilities.GetSimpleHashCode(material.name);
            spriteAsset.spriteGlyphTable.Clear();
            spriteAsset.spriteCharacterTable.Clear();
            spriteAsset.spriteInfoList = new List<TMP_Sprite>();

            for (var i = 0; i < images.Count; i++)
            {
                var rect = rects[i];
                var x = Mathf.RoundToInt(rect.x * atlas.width);
                var y = Mathf.RoundToInt(rect.y * atlas.height);
                var width = Mathf.Max(1, Mathf.RoundToInt(rect.width * atlas.width));
                var height = Mathf.Max(1, Mathf.RoundToInt(rect.height * atlas.height));
                var aspectRatio = width / (float)height;
                var glyph = new TMP_SpriteGlyph(
                    (uint)i,
                    new GlyphMetrics(aspectRatio, 1f, 0f, 1f, aspectRatio),
                    new GlyphRect(x, y, width, height),
                    1f,
                    0);
                spriteAsset.spriteGlyphTable.Add(glyph);

                var character = new TMP_SpriteCharacter(0xFFFE, glyph)
                {
                    name = images[i].Name,
                    scale = 1f
                };
                spriteAsset.spriteCharacterTable.Add(character);
            }

            spriteAsset.UpdateLookupTables();
            return new InlineImageAssetBundle(node, atlas, material, spriteAsset);
        }
        catch
        {
            DestroyImmediate(spriteAsset);
            DestroyImmediate(material);
            DestroyImmediate(atlas);
            throw;
        }
        finally
        {
            foreach (var image in images)
                DestroyImmediate(image.Texture);
        }
    }

    private static Texture2D DownloadImage(string imageUrl)
    {
        var requestUrl = GetRequestUrl(imageUrl);
        using (var client = new WebClient())
        {
            client.Headers[HttpRequestHeader.UserAgent] = "WiresMerge Unity Editor";
            var bytes = client.DownloadData(requestUrl);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                DestroyImmediate(texture);
                throw new InvalidDataException($"The URL did not return a valid image: {imageUrl}");
            }

            return texture;
        }
    }

    private static string GetRequestUrl(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException("Image URL must be an absolute HTTP or HTTPS URL.");
        }

        if (uri.Host.Equals("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            var fileId = GetDriveFileId(uri);
            if (!string.IsNullOrEmpty(fileId))
                return $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(fileId)}";
        }

        return uri.AbsoluteUri;
    }

    private static string GetDriveFileId(Uri uri)
    {
        var pathSegments = uri.AbsolutePath.Split('/');
        for (var i = 0; i < pathSegments.Length - 1; i++)
        {
            if (pathSegments[i].Equals("d", StringComparison.OrdinalIgnoreCase) &&
                i > 0 && pathSegments[i - 1].Equals("file", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < pathSegments.Length)
            {
                return Uri.UnescapeDataString(pathSegments[i + 1]);
            }
        }

        foreach (var queryPart in uri.Query.TrimStart('?').Split('&'))
        {
            var separator = queryPart.IndexOf('=');
            if (separator <= 0 ||
                !queryPart.Substring(0, separator).Equals("id", StringComparison.OrdinalIgnoreCase))
                continue;

            return Uri.UnescapeDataString(queryPart.Substring(separator + 1));
        }

        return null;
    }

    private void AddInlineImageAssetsToGraph(List<InlineImageAssetBundle> bundles)
    {
        foreach (var bundle in bundles)
        {
            AssetDatabase.AddObjectToAsset(bundle.Atlas, _graph);
            AssetDatabase.AddObjectToAsset(bundle.Material, _graph);
            AssetDatabase.AddObjectToAsset(bundle.SpriteAsset, _graph);
        }
    }

    private void RemoveGeneratedAssetsNotIn(List<InlineImageAssetBundle> bundles)
    {
        var retained = new HashSet<UnityEngine.Object>();
        foreach (var bundle in bundles)
        {
            retained.Add(bundle.Atlas);
            retained.Add(bundle.Material);
            retained.Add(bundle.SpriteAsset);
        }

        var path = AssetDatabase.GetAssetPath(_graph);
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            if (asset == _graph ||
                !asset.name.StartsWith(GeneratedAssetPrefix, StringComparison.Ordinal) ||
                retained.Contains(asset))
                continue;

            AssetDatabase.RemoveObjectFromAsset(asset);
            DestroyImmediate(asset, true);
        }
    }

    private static void DestroyUnattachedBundles(List<InlineImageAssetBundle> bundles)
    {
        foreach (var bundle in bundles)
        {
            if (AssetDatabase.Contains(bundle.SpriteAsset))
                continue;

            DestroyImmediate(bundle.SpriteAsset);
            DestroyImmediate(bundle.Material);
            DestroyImmediate(bundle.Atlas);
        }
    }

    private sealed class InlineImage
    {
        public readonly string Name;
        public readonly Texture2D Texture;

        public InlineImage(string name, Texture2D texture)
        {
            Name = name;
            Texture = texture;
        }
    }

    private sealed class InlineImageAssetBundle
    {
        public readonly DialogNode Node;
        public readonly Texture2D Atlas;
        public readonly Material Material;
        public readonly TMP_SpriteAsset SpriteAsset;

        public InlineImageAssetBundle(
            DialogNode node,
            Texture2D atlas,
            Material material,
            TMP_SpriteAsset spriteAsset)
        {
            Node = node;
            Atlas = atlas;
            Material = material;
            SpriteAsset = spriteAsset;
        }
    }

    private void CreateNewDialogGraph()
    {
        var newGraph = CreateInstance<DialogGraph>();

        var path = EditorUtility.SaveFilePanelInProject(
            "Create Dialog Graph",
            "DialogGraph",
            "asset",
            "Save Dialog Graph");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(newGraph, path);
            AssetDatabase.SaveAssets();
            _graph = newGraph;
            Repaint();
        }
    }
}
#endif
