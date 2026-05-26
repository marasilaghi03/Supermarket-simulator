
using System.Collections.Generic;
using UnityEngine;

public partial class CustomerAgent
{
    private class DecisionContext
    {
        public ProductType Wanted = ProductType.None;
        public GridCell ChosenShelf = null;
        public List<ShelfOption> Options = new List<ShelfOption>();
        public int OtherItemIndex = -1;
    }

    private class DecisionTreeNode
    {
        public string Name;
        public bool IsLeaf;

        public Condition Condition;
        public int TrueNode;
        public int FalseNode;

        public Decision Decision;
        public string Note;
    }

    private void AddConditionNode(string name, Condition condition, int trueNode, int falseNode)
    {
        generatedDecisionTree.Add(new DecisionTreeNode
        {
            Name = name,
            IsLeaf = false,
            Condition = condition,
            TrueNode = trueNode,
            FalseNode = falseNode
        });
    }

    private void AddLeafNode(string name, Decision decision, string noteText)
    {
        generatedDecisionTree.Add(new DecisionTreeNode
        {
            Name = name,
            IsLeaf = true,
            Decision = decision,
            Note = noteText
        });
    }


    //parcurge arborele de decizie
    private Decision DecideNextAction(out ProductType wanted, out GridCell chosenShelf)
    {
        wanted = ProductType.None;
        chosenShelf = null;

        if (generatedDecisionTree == null || generatedDecisionTree.Count == 0)
            GenerateDecisionTree();

        if (!HasFinishedShopping())
        {
            if (currentItemIndex < 0 || currentItemIndex >= shoppingList.Count)
                currentItemIndex = 0;
        }

        DecisionContext context = new DecisionContext();

        //seteaza produsul curent
        if (!HasFinishedShopping())
            context.Wanted = shoppingList[currentItemIndex];

        //parcurge arborele de decizie pana la o frunza
        int nodeIndex = 0;

        while (nodeIndex >= 0 && nodeIndex < generatedDecisionTree.Count)
        {
            DecisionTreeNode node = generatedDecisionTree[nodeIndex];

            //daca e frunza, returneaza decizia
            if (node.IsLeaf)
            {
                note = node.Note;

                ApplyDecisionSideEffects(node.Decision, context);

                wanted = context.Wanted;
                chosenShelf = context.ChosenShelf;

                return node.Decision;
            }

            // altfel, e nod de conditie, evalueaza conditia si merge pe ramura corespunzatoare
            bool conditionResult = EvaluateCondition(node.Condition, context);
            nodeIndex = conditionResult ? node.TrueNode : node.FalseNode;
        }

        note = "Decision tree error";
        return Decision.WaitForShelf;
    }

    //verifica o conditie specifica pentru a decide ce ramura sa urmeze
    private bool EvaluateCondition(Condition condition, DecisionContext context)
    {
        switch (condition)
        {
            case Condition.FinishedShopping:
                return HasFinishedShopping();

            case Condition.ProductMissing:
                return grid.FindShelves(context.Wanted).Count == 0;

            case Condition.HasShelfOptions:
                context.Options = GetShelfOptions(context.Wanted);
                return context.Options.Count > 0;

            case Condition.HasNearShelf:
                {
                    ShelfOption nearOption = GetClosestOption(context.Options, onlyNear: true);

                    if (nearOption == null)
                        return false;

                    context.ChosenShelf = nearOption.Shelf;
                    return true;
                }

            case Condition.HasClosestShelf:
                {
                    ShelfOption bestOption = GetClosestOption(context.Options, onlyNear: false);

                    if (bestOption == null)
                        return false;

                    context.ChosenShelf = bestOption.Shelf;
                    return true;
                }

            case Condition.WaitedTooLong:
                return shelfWaitTimer >= maxShelfWaitTime;

            case Condition.HasOtherAvailableItem:
                {
                    int otherItemIndex = FindAvailableItemIndex();

                    if (otherItemIndex == -1)
                        return false;

                    context.OtherItemIndex = otherItemIndex;
                    return true;
                }
        }

        return false;
    }

    //aplica efectele secundare ale unei decizii, cum ar fi resetarea timerelor sau schimbarea produsului curent
    private void ApplyDecisionSideEffects(Decision decision, DecisionContext context)
    {
        switch (decision)
        {
            case Decision.GoToQueue:
                shelfWaitTimer = 0f;
                break;

            case Decision.GoToShelf:
                shelfWaitTimer = 0f;
                break;

            case Decision.TryOtherItem:
                if (context.OtherItemIndex != -1)
                {
                    currentItemIndex = context.OtherItemIndex;
                    shelfWaitTimer = 0f;
                    itemSwitches++;
                }
                break;

            case Decision.SkipItem:
                break;

            case Decision.WaitForShelf:
                break;
        }
    }
}
