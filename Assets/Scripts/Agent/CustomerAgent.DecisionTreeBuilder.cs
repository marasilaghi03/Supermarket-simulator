using System.Collections.Generic;

public partial class CustomerAgent
{
    private class DecisionTreeBuilder
    {
        public abstract class BuildNode { }

        public class ConditionBuildNode : BuildNode
        {
            public Condition Condition;
            public BuildNode TrueBranch;
            public BuildNode FalseBranch;
        }

        public class LeafBuildNode : BuildNode
        {
            public Decision Decision;
            public string Note;
        }

        private BuildNode root;

        public static LeafBuildNode Leaf(Decision decision, string note = "")
        {
            return new LeafBuildNode { Decision = decision, Note = note };
        }

        public static ConditionBuildNode Branch(Condition condition, BuildNode trueBranch, BuildNode falseBranch)
        {
            return new ConditionBuildNode
            {
                Condition = condition,
                TrueBranch = trueBranch,
                FalseBranch = falseBranch
            };
        }

        public void SetRoot(BuildNode node)
        {
            root = node;
        }

        public List<DecisionTreeNode> Build()
        {
            List<DecisionTreeNode> nodes = new List<DecisionTreeNode>();
            Compile(root, nodes);
            return nodes;
        }

        private int Compile(BuildNode node, List<DecisionTreeNode> nodes)
        {
            if (node is LeafBuildNode leaf)
            {
                int index = nodes.Count;
                nodes.Add(new DecisionTreeNode
                {
                    Name = leaf.Decision.ToString(),
                    IsLeaf = true,
                    Decision = leaf.Decision,
                    Note = leaf.Note
                });
                return index;
            }

            if (node is ConditionBuildNode condition)
            {
                int index = nodes.Count;
                nodes.Add(null);

                int trueIndex = Compile(condition.TrueBranch, nodes);
                int falseIndex = Compile(condition.FalseBranch, nodes);

                nodes[index] = new DecisionTreeNode
                {
                    Name = condition.Condition.ToString(),
                    IsLeaf = false,
                    Condition = condition.Condition,
                    TrueNode = trueIndex,
                    FalseNode = falseIndex
                };

                return index;
            }

            return 0;
        }
    }

    private void GenerateDecisionTree()
    {
        var b = new DecisionTreeBuilder();

        b.SetRoot(
            DecisionTreeBuilder.Branch(Condition.FinishedShopping,      //motiv
                trueBranch: DecisionTreeBuilder.Leaf(Decision.GoToQueue, "Shopping finished"),   //decizie
                falseBranch: DecisionTreeBuilder.Branch(Condition.ProductMissing,
                    trueBranch: DecisionTreeBuilder.Leaf(Decision.SkipItem, "Product not in shop"),
                    falseBranch: DecisionTreeBuilder.Branch(Condition.HasShelfOptions,
                        trueBranch: DecisionTreeBuilder.Branch(Condition.HasNearShelf,
                            trueBranch: DecisionTreeBuilder.Leaf(Decision.GoToShelf, "Shelf is nearby"),
                            falseBranch: DecisionTreeBuilder.Branch(Condition.HasClosestShelf,
                                trueBranch: DecisionTreeBuilder.Leaf(Decision.GoToShelf, "Going to closest shelf"),
                                falseBranch: DecisionTreeBuilder.Branch(Condition.WaitedTooLong,
                                    trueBranch: DecisionTreeBuilder.Branch(Condition.HasOtherAvailableItem,
                                        trueBranch: DecisionTreeBuilder.Leaf(Decision.TryOtherItem, "Switching to other item"),
                                        falseBranch: DecisionTreeBuilder.Leaf(Decision.WaitForShelf, "No other item, waiting")),
                                    falseBranch: DecisionTreeBuilder.Leaf(Decision.WaitForShelf, "Haven't waited long enough")))),
                        falseBranch: DecisionTreeBuilder.Branch(Condition.WaitedTooLong,
                            trueBranch: DecisionTreeBuilder.Branch(Condition.HasOtherAvailableItem,
                                trueBranch: DecisionTreeBuilder.Leaf(Decision.TryOtherItem, "No shelf options, switching item"),
                                falseBranch: DecisionTreeBuilder.Leaf(Decision.WaitForShelf, "No shelf options, no other item")),
                            falseBranch: DecisionTreeBuilder.Leaf(Decision.WaitForShelf, "No shelf options, waiting"))))));

        generatedDecisionTree = b.Build();
    }
}