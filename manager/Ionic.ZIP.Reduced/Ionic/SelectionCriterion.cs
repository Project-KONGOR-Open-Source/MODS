using Ionic.Zip;

namespace Ionic;

internal abstract class SelectionCriterion
{
	internal abstract bool Evaluate(string filename);

	internal abstract bool Evaluate(ZipEntry entry);
}
