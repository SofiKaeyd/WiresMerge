using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogGraph", menuName = "Dialog System/Dialog Graph")]
public class DialogGraph : ScriptableObject
{
    public List<DialogRelation> Relations = new List<DialogRelation>();
    public List<DialogNode> Nodes = new List<DialogNode>();
    public int QuestionsCount = 0;

    public void Set(DialogGraph graph)
    {
        Relations = graph.Relations;
        Nodes = graph.Nodes;
        QuestionsCount = graph.QuestionsCount;
    }

    public DialogNode GetNode(int id)
    {
        return Nodes.Count > id ? Nodes[id] : null;
    }

    public List<DialogNode> GetChildren(DialogNode node)
    {
        var children = new List<DialogNode>();
        foreach (var childId in node.ChildrenIds)
        {
            var child = GetNode(childId);
            if (child != null)
                children.Add(child);
        }

        return children;
    }

    public List<DialogNode> GetParents(DialogNode node)
    {
        var parents = new List<DialogNode>();
        foreach (var parentId in node.ParentIds)
        {
            var parent = GetNode(parentId);
            if (parent != null)
                parents.Add(parent);
        }

        return parents;
    }

    public void ClearData()
    {
        Relations.Clear();
        Nodes.Clear();
        QuestionsCount = 0;
    }
}

[Serializable]
public class DialogNode
{
    public const string SourceImageSpriteName = "SourceImage";
    public const string RecipientImageSpriteName = "RecipientImage";
    public const string StartAnimation1Parameter = "startAnimation1";
    public const string StartAnimation2Parameter = "startAnimation2";
    public const string StartAnimation3Parameter = "startAnimation3";
    public const string StartAnimation4Parameter = "startAnimation4";
    public const string LevelBeginParameter = "LevelBegin";

    public int Id;
    public string Speaker;
    public string Text;
    public bool IsPlayer;
    public string Parameters;
    public string Conditions;
    public bool IsCorrect;
    public string SourceImageUrl;
    public string RecipientImageUrl;
    public TMP_SpriteAsset InlineImageAsset;

    public List<int> ParentIds = new List<int>();
    public List<int> ChildrenIds = new List<int>();

    public bool HasInlineImages =>
        !string.IsNullOrWhiteSpace(SourceImageUrl) || !string.IsNullOrWhiteSpace(RecipientImageUrl);

    public bool HasParameter(string parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return false;

        foreach (var name in Parameters.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(name.Trim(), parameter, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public string FormattedText
    {
        get
        {
            var args = new List<object>();

            if (HasInlineImages)
            {
                args.Add(string.IsNullOrWhiteSpace(SourceImageUrl)
                    ? ""
                    : $"<sprite name=\"{SourceImageSpriteName}\">");
                args.Add(string.IsNullOrWhiteSpace(RecipientImageUrl)
                    ? ""
                    : $"<sprite name=\"{RecipientImageSpriteName}\">");
            }

            var paramNames = Parameters.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var name in paramNames)
            {
                var trimmedName = name.Trim();
                if (trimmedName == StartAnimation1Parameter ||                   
                    trimmedName == StartAnimation2Parameter ||
                    trimmedName == StartAnimation3Parameter ||
                    trimmedName == StartAnimation4Parameter ||
                    trimmedName == LevelBeginParameter)
                    continue;

                args.Add(DynamicParameters.Get(name));
            }

            return string.Format(Text, args.ToArray());
        }
    }

    public string FormattedTextWithImageFallback
    {
        get
        {
            var text = FormattedText;
            if (!string.IsNullOrWhiteSpace(SourceImageUrl))
                text = text.Replace($"<sprite name=\"{SourceImageSpriteName}\">", "[Источник недоступен]");
            if (!string.IsNullOrWhiteSpace(RecipientImageUrl))
                text = text.Replace($"<sprite name=\"{RecipientImageSpriteName}\">", "[Получатель недоступен]");

            return text;
        }
    }

    public bool Available
    {
        get
        {
            var conditionNames = Conditions.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var args = new List<object>();
            foreach (var name in conditionNames)
                args.Add(DynamicParameters.Get(name));

            return args.All(a => a.Equals(true));
        }
    }

    public DialogNode(
        int nodeId,
        string speaker,
        string text,
        bool isPlayer,
        string parameters,
        string conditions,
        bool isCorrect,
        string sourceImageUrl = null,
        string recipientImageUrl = null)
    {
        Id = nodeId;
        Speaker = speaker;
        Text = text;
        IsPlayer = isPlayer;
        Parameters = parameters;
        Conditions = conditions;
        IsCorrect = isCorrect;
        SourceImageUrl = sourceImageUrl;
        RecipientImageUrl = recipientImageUrl;
    }
}

[Serializable]
public class DialogRelation
{
    public int ParentID;
    public int ChildID;

    public DialogRelation(int parentId, int childId)
    {
        ParentID = parentId;
        ChildID = childId;
    }
}
