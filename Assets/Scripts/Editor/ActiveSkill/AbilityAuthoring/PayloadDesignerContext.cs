// Read context passed to descriptor calls. Node is null when a descriptor is asked to describe
// an always-active (blank-gated) payload that is not owned by any single node -- see
// NODE_CENTRIC_PAYLOAD_AUTHORING_PLAN.md section 14.4.
public sealed class PayloadDesignerContext
{
    public PayloadDesignerContext(SkillUpgradeTreeDefinition tree, SkillGemDefinition owner, SkillUpgradeNodeData node)
    {
        Tree = tree;
        Owner = owner;
        Node = node;
    }

    public SkillUpgradeTreeDefinition Tree { get; }
    public SkillGemDefinition Owner { get; }
    public SkillUpgradeNodeData Node { get; }
}
