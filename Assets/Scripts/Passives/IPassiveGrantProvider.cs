using System.Collections.Generic;

public interface IPassiveGrantProvider
{
    void AppendPassiveGrants(List<PassiveGrant> buffer);
}
