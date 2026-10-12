namespace OddFrame.Core;

public interface IStateful
{
    void Save(BinaryWriter writer);
    void Load(BinaryReader reader);
}