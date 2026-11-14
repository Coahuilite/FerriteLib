param([Parameter(Mandatory = $true)][string]$Path)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Read metadata without resolving/loading the game's dependencies into this process.
$stream = [IO.File]::OpenRead($Path)
try {
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
    try {
        $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
        $assembly = $reader.GetAssemblyDefinition()
        $attributes = @{}
        foreach ($handle in $assembly.GetCustomAttributes()) {
            $attribute = $reader.GetCustomAttribute($handle)
            if ($attribute.Constructor.Kind -ne 'MemberReference') { continue }
            $member = $reader.GetMemberReference([System.Reflection.Metadata.Ecma335.MetadataTokens]::MemberReferenceHandle(
                [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($attribute.Constructor)))
            if ($member.Parent.Kind -ne 'TypeReference') { continue }
            $type = $reader.GetTypeReference([System.Reflection.Metadata.Ecma335.MetadataTokens]::TypeReferenceHandle(
                [System.Reflection.Metadata.Ecma335.MetadataTokens]::GetRowNumber($member.Parent)))
            $name = $reader.GetString($type.Namespace) + '.' + $reader.GetString($type.Name)
            if ($name -notin @('System.Reflection.AssemblyConfigurationAttribute',
                'System.Reflection.AssemblyInformationalVersionAttribute', 'System.Runtime.Versioning.TargetFrameworkAttribute')) { continue }
            $blob = $reader.GetBlobReader($attribute.Value)
            if ($blob.ReadUInt16() -ne 1) { throw 'Invalid carrier attribute encoding.' }
            $attributes[$name] = $blob.ReadSerializedString()
        }
        [ordered]@{
            name = $reader.GetString($assembly.Name)
            version = $assembly.Version.ToString()
            mvid = $reader.GetGuid($reader.GetModuleDefinition().Mvid).ToString()
            configuration = $attributes['System.Reflection.AssemblyConfigurationAttribute']
            informationalVersion = $attributes['System.Reflection.AssemblyInformationalVersionAttribute']
            framework = $attributes['System.Runtime.Versioning.TargetFrameworkAttribute']
            references = @(foreach ($handle in $reader.AssemblyReferences) {
                $reference = $reader.GetAssemblyReference($handle)
                $reader.GetString($reference.Name)
            })
        } | ConvertTo-Json -Depth 4
    } finally { $pe.Dispose() }
} finally { $stream.Dispose() }
