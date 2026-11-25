using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DialogSystem
{
    private DialogGraph _graph;

    public int QuestionsCount => _graph.QuestionsCount;
    public DialogNode CurrentRootNode { get; protected set; }
    public List<DialogNode> NextDialogNodes { get; protected set; } = new List<DialogNode>();

    public Action OnNextStep { get; set; }

    public bool IsTerminalChoice(DialogNode node)
    {
        return node != null && node.IsPlayer && Config.GetChildren(node).Count == 0;
    }

    protected DialogGraph Config
    {
        get
        {
            if (_graph == null)
                _graph = Resources.Load<DialogGraph>("Dialogs/DialogGraph");

            return _graph;
        }
    }

    public void Start(int nodeId)
    {
        NextStep(Config.GetNode(nodeId));
    }

    public virtual bool NextStep(DialogNode node = null)
    {
        if (node == null)
            return false;

        LevelManager.RecordDialogNode(node);

        var children = Config.GetChildren(node);
        if (IsTerminalChoice(node))
            return false;

        Debug.Log(node.Id);

        CurrentRootNode = node;
        NextDialogNodes = children;
        if (CurrentRootNode.IsPlayer && NextDialogNodes.Any(n => !n.IsPlayer))
            return NextPersNode();
        else
            OnNextStep?.Invoke();

        LevelManager.LastDialogNodeId = node.Id;
        return true;
    }

    public bool NextPersNode()
    {
        return NextStep(NextDialogNodes.FirstOrDefault(n => n.Available));
    }
}

public class QuizSystem : DialogSystem
{
    public override bool NextStep(DialogNode node = null)
    {
        if (node == null)
            return false;

        var children = Config.GetChildren(node);
        if (node.IsPlayer)
            return node.IsCorrect;

        Debug.Log(node.Id);

        CurrentRootNode = node;
        NextDialogNodes = children;

        return true;
    }
}
