using System.Collections.Generic;

namespace PWTA
{
    public interface IFoliageFileSchema
    {
        int Version { get; }
        int Signature { get; }        
        int FoliageCount { get; }
        List<IFoliageElement> Foliages { get; }
    }
}