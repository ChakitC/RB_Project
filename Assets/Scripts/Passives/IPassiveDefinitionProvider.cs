using System.Collections.Generic;

public interface IPassiveDefinitionProvider
{
    void AppendPassiveDefinitions(List<PassiveDefinition> buffer);
}
