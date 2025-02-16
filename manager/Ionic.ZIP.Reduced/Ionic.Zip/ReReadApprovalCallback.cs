namespace Ionic.Zip;

public delegate bool ReReadApprovalCallback(long uncompressedSize, long compressedSize, string fileName);
