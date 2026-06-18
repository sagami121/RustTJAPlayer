using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TjaPlayer.Utils
{
    internal static class CompanionFileFinder
    {
        internal static string FindFileName(
            string directory,
            string mainFileName,
            string expectedCompanionFileName)
        {
            var expectedCompanionPath = Path.Combine(directory, expectedCompanionFileName);

            if (File.Exists(expectedCompanionPath))
            {
                return expectedCompanionFileName;
            }

            // If we could not find the file by its exact provided name, in
            // the vast majority of cases it has been mangled during zip
            // compression by a zip tool which is not properly aware of
            // multi-byte encodings, Unicode, etc. When decompressed, such
            // zipped files end up a file names which are simply the raw bytes
            // of the Shift-JIS encoded form. Some of these bytes will be
            // invalid as characters of file names and will have been further
            // mangled, usually to a single underscore character.

            // To begin finding the right file, we first need to get the raw
            // bytes that would comprise the file name if encoded into
            // Shift-JIS.
            // Ensure encoding provider is registered for Shift_JIS
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encodedCompanionFileNameBytes = Encoding.GetEncoding("Shift_JIS").GetBytes(expectedCompanionFileName);

            // Attempt to find the file as if the companion file's name was
            // mangled into codepage 437 (effectively the legacy DOS codepage,
            // and the one used by zip tools that are not unicode aware.)
            // This step finds >99% of files with mangled names.
            if (TryFindViaDecodedFileName(
                directory,
                encodedCompanionFileNameBytes,
                "Encoding.GetEncoding(437)",
                Encoding.GetEncoding(437),
                out var foundCompanionFileNameViaEncoding437))
            {
                return foundCompanionFileNameViaEncoding437!;
            }

            // Attempt to find the file as if the companion file's name
            // was mangled into this computer's default encoding.
            if (TryFindViaDecodedFileName(
                directory,
                encodedCompanionFileNameBytes,
                "Encoding.Default",
                Encoding.Default,
                out var foundCompanionFileNameViaEncodingDefault))
            {
                return foundCompanionFileNameViaEncodingDefault!;
            }

            // If the companion file still cannot be found, try to find a file
            // with the expected extension but having the same file name as the
            // main file with which it is associated (in most use cases: the .tja file.)
            if (TryFindViaMainFileName(
                directory,
                mainFileName,
                expectedCompanionPath,
                out var foundCompanionFileNameByMainFileName))
            {
                return foundCompanionFileNameByMainFileName!;
            }

            // If the companion file still cannot be found, try to find a single file
            // with the expected supplementary file extension.
            if (TryFindViaCompanionFileExtension(
                directory,
                expectedCompanionPath,
                out var foundCompanionFileNameByExtension))
            {
                return foundCompanionFileNameByExtension!;
            }

            // If the file still cannot be found, produce a warning
            // and return the original file name unchanged.

            Trace.TraceWarning(
                $"{nameof(CompanionFileFinder)} could not find expected file '{expectedCompanionPath}' by any available means.");

            return expectedCompanionFileName;
        }

        private static bool TryFindViaDecodedFileName(
            string directory,
            byte[] encodedBytes,
            string prefix,
            Encoding encoding,
            out string? foundCompanionFileName)
        {
            var decodedCompanionFileName = DecodeToLegalFileName(encodedBytes, encoding);

            try
            {
                if (File.Exists(Path.Combine(directory, decodedCompanionFileName)))
                {
                    Trace.TraceInformation(
                        $"{nameof(CompanionFileFinder)} found '{decodedCompanionFileName}' via {prefix} '{encoding.EncodingName}'.");
                    foundCompanionFileName = decodedCompanionFileName;
                    return true;
                }
            }
            catch
            {
                Trace.TraceWarning(
                    $"{nameof(CompanionFileFinder)} could not check existence via {prefix} '{encoding.EncodingName}'.");
            }

            foundCompanionFileName = string.Empty;
            return false;
        }

        private static string DecodeToLegalFileName(byte[] encodedBytes, Encoding encoding)
        {
            var decodedBeforeDirectoryRemoval = encoding.GetString(encodedBytes)
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(':', '_')
                .Replace('"', '_')
                .Replace('/', '_')
                .Replace('|', '_')
                .Replace('?', '_')
                .Replace('*', '_');

            var lastIndexOfBackslash = decodedBeforeDirectoryRemoval.LastIndexOf('\\');
            return lastIndexOfBackslash == -1
                ? decodedBeforeDirectoryRemoval
                : decodedBeforeDirectoryRemoval.Substring(lastIndexOfBackslash + 1);
        }

        private static bool TryFindViaMainFileName(
            string directory,
            string mainFileName,
            string expectedCompanionPath,
            out string? foundCompanionFileName)
        {
            var mainFilePath = Path.Combine(directory, mainFileName);
            var companionFileExtension = Path.GetExtension(expectedCompanionPath);
            var mainFilePathWithCompanionFileExtension = Path.ChangeExtension(mainFilePath, companionFileExtension);
            var mainFileNameWithCompanionFileExtension = Path.GetFileName(mainFilePathWithCompanionFileExtension);
            
            if (File.Exists(mainFilePathWithCompanionFileExtension))
            {
                foundCompanionFileName = mainFileNameWithCompanionFileExtension;
                return true;
            }

            foundCompanionFileName = string.Empty;
            return false;
        }

        private static bool TryFindViaCompanionFileExtension(
            string directory,
            string expectedCompanionPath,
            out string? foundCompanionFileName)
        {
            var companionFileExtension = Path.GetExtension(expectedCompanionPath);

            if (!string.IsNullOrEmpty(companionFileExtension))
            {
                var filesWithTheCompanionFileExtension = Directory.GetFiles(directory, "*" + companionFileExtension);
                if (filesWithTheCompanionFileExtension.Length == 1)
                {
                    foundCompanionFileName = Path.GetFileName(filesWithTheCompanionFileExtension[0]);
                    return true;
                }
            }

            foundCompanionFileName = string.Empty;
            return false;
        }
    }
}
