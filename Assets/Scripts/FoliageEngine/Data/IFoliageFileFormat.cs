using System.IO;

namespace PWTA
{
    public interface IFoliageSerializer
    { 
        int Version {get;}
        int Signature {get;}

        void Save(Stream writeStream, IFoliageFileSchema data);
        
        IFoliageFileSchema Load(Stream fileStream);        
    }
}