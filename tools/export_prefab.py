#!/usr/bin/env python3
"""
Unity Prefab Cross-Project Exporter

Exports a Unity prefab with all its dependencies (materials, textures, meshes,
shaders) from one project to another.

Features:
  - Recursive dependency resolution (prefab -> materials -> textures/shaders)
  - Skips assets that already exist in the target project (by GUID)
  - Detects GUID collisions (same GUID, different asset) and remaps automatically
  - Auto-detects MonoBehaviour script references in the entire chain
  - Matches source scripts to target project scripts by class name (exact + fuzzy)
  - Auto-detects serialized field renames between matched scripts
  - Manual overrides via --swap-script and --rename-field
  - Applies all swaps/renames across ALL files in the chain (not just root prefab)

Usage:
    python export_prefab.py \
        --source-project "C:\\Voodoo\\Paper2" \
        --target-project "C:\\Voodoo\\Playable_MD" \
        --prefab "Assets/_Paper2/Features/X/Prefabs/MyPrefab.prefab" \
        --target-dir "C:\\Voodoo\\Playable_MD\\Assets\\Playable\\VFX\\test" \
        --swap-script "OLD_GUID:NEW_GUID" \
        --rename-field "_oldField:_newField" \
        --force
"""

import argparse
import os
import re
import shutil
import sys
import uuid
from difflib import SequenceMatcher
from pathlib import Path


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

BUILTIN_GUID_PREFIXES = {
    "0000000000000000",
}

BUILTIN_GUIDS = {
    "0000000000000000f000000000000000",
    "0000000000000000e000000000000000",
    "0000000000000000d000000000000000",
    "0000000000000000c000000000000000",
    "0000000000000000b000000000000000",
    "0000000000000000a000000000000000",
}

PARSEABLE_EXTENSIONS = {
    ".mat", ".prefab", ".asset", ".controller", ".anim",
    ".shadergraph", ".shadersubgraph", ".shader",
    ".overrideController", ".mask", ".flare", ".renderTexture",
    ".lighting", ".giparams", ".spriteatlasv2", ".spriteatlas",
}

GUID_PATTERN = re.compile(r'guid:\s*([0-9a-f]{32})')
META_GUID_PATTERN = re.compile(r'^guid:\s*([0-9a-f]{32})', re.MULTILINE)

# Matches: class Foo : MonoBehaviour  /  class Foo : SomeBase  /  class Foo
CLASS_NAME_PATTERN = re.compile(
    r'^\s*(?:public\s+|internal\s+|abstract\s+|sealed\s+|partial\s+)*'
    r'class\s+(\w+)',
    re.MULTILINE,
)

# Matches serialized fields:
#   [SerializeField] private int _foo;
#   [SerializeField] int _foo;
#   public int Foo;
#   public List<Renderer> _renderers;
SERIALIZED_FIELD_PATTERN = re.compile(
    r'(?:'
    r'\[SerializeField\]\s*(?:private|protected|internal)?\s*'  # [SerializeField] ...
    r'|public\s+'                                               # or public
    r')'
    r'([\w<>\[\],\s]+?)\s+'   # type (greedy but lazy enough)
    r'(\w+)\s*'               # field name
    r'(?:=\s*[^;]+)?'         # optional default value
    r'\s*;',                  # semicolon
    re.MULTILINE,
)

FUZZY_MATCH_THRESHOLD = 0.55  # SequenceMatcher ratio


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def is_builtin_guid(guid: str) -> bool:
    if guid in BUILTIN_GUIDS:
        return True
    for prefix in BUILTIN_GUID_PREFIXES:
        if guid.startswith(prefix):
            return True
    return False


def generate_unity_guid() -> str:
    return uuid.uuid4().hex


def build_guid_index(project_root: Path, label: str = "") -> dict[str, Path]:
    display = label or str(project_root)
    print(f"Building GUID index for {display}...")
    index = {}
    assets_dir = project_root / "Assets"
    if not assets_dir.exists():
        print(f"  ERROR: Assets directory not found at {assets_dir}")
        return index

    meta_count = 0
    for meta_path in assets_dir.rglob("*.meta"):
        meta_count += 1
        try:
            content = meta_path.read_text(encoding="utf-8", errors="replace")
            match = META_GUID_PATTERN.search(content)
            if match:
                index[match.group(1)] = meta_path.with_suffix("")
        except (OSError, UnicodeDecodeError):
            pass

    print(f"  Indexed {len(index)} GUIDs from {meta_count} .meta files")
    return index


def extract_guids(file_path: Path) -> set[str]:
    guids = set()
    try:
        content = file_path.read_text(encoding="utf-8", errors="replace")
        for match in GUID_PATTERN.finditer(content):
            guid = match.group(1)
            if not is_builtin_guid(guid):
                guids.add(guid)
    except (OSError, UnicodeDecodeError) as e:
        print(f"  WARNING: Could not read {file_path}: {e}")
    return guids


# ---------------------------------------------------------------------------
# C# script analysis
# ---------------------------------------------------------------------------

def extract_class_name(cs_path: Path) -> str | None:
    """Extract the primary class name from a .cs file."""
    try:
        content = cs_path.read_text(encoding="utf-8", errors="replace")
        match = CLASS_NAME_PATTERN.search(content)
        return match.group(1) if match else None
    except (OSError, UnicodeDecodeError):
        return None


def extract_serialized_fields(cs_path: Path) -> list[tuple[str, str]]:
    """Extract (type, name) pairs for serialized fields from a .cs file."""
    fields = []
    try:
        content = cs_path.read_text(encoding="utf-8", errors="replace")
        for match in SERIALIZED_FIELD_PATTERN.finditer(content):
            field_type = re.sub(r'\s+', ' ', match.group(1).strip())
            field_name = match.group(2)
            fields.append((field_type, field_name))
    except (OSError, UnicodeDecodeError):
        pass
    return fields


def build_class_index(project_root: Path) -> dict[str, tuple[str, Path]]:
    """Build class_name -> (guid, cs_path) index for all .cs files in a project."""
    index = {}
    assets_dir = project_root / "Assets"
    if not assets_dir.exists():
        return index
    for cs_path in assets_dir.rglob("*.cs"):
        class_name = extract_class_name(cs_path)
        if class_name is None:
            continue
        # Read the GUID from the .meta file
        meta_path = Path(str(cs_path) + ".meta")
        if not meta_path.exists():
            continue
        try:
            meta_content = meta_path.read_text(encoding="utf-8", errors="replace")
            match = META_GUID_PATTERN.search(meta_content)
            if match:
                index[class_name] = (match.group(1), cs_path)
        except (OSError, UnicodeDecodeError):
            pass
    return index


def find_script_match(
    source_class: str,
    target_class_index: dict[str, tuple[str, Path]],
) -> tuple[str, str, Path] | None:
    """
    Find a matching script in the target project for a source class name.

    Returns (match_type, target_guid, target_cs_path) or None.
    Match types: "exact", "substring", "fuzzy"
    """
    # 1. Exact match
    if source_class in target_class_index:
        guid, path = target_class_index[source_class]
        return ("exact", guid, path)

    # 2. Substring match — source contains target or vice versa
    #    e.g. "PlayerVfxInstanceView" contains "VfxInstanceView"
    best_substring = None
    best_sub_len = 0
    for target_class, (guid, path) in target_class_index.items():
        if target_class in source_class or source_class in target_class:
            common_len = min(len(target_class), len(source_class))
            if common_len > best_sub_len:
                best_sub_len = common_len
                best_substring = ("substring", guid, path, target_class)

    if best_substring:
        return best_substring[:3]

    # 3. Fuzzy match
    best_fuzzy = None
    best_ratio = 0.0
    for target_class, (guid, path) in target_class_index.items():
        ratio = SequenceMatcher(None, source_class.lower(), target_class.lower()).ratio()
        if ratio > best_ratio and ratio >= FUZZY_MATCH_THRESHOLD:
            best_ratio = ratio
            best_fuzzy = ("fuzzy", guid, path, target_class, ratio)

    if best_fuzzy:
        return best_fuzzy[:3]

    return None


def detect_field_renames(
    source_fields: list[tuple[str, str]],
    target_fields: list[tuple[str, str]],
) -> list[tuple[str, str]]:
    """
    Compare serialized fields between source and target scripts.
    Detect renames: same type, same position, different name.

    Returns list of (old_name, new_name) pairs.
    """
    renames = []

    # Build type->name maps for quick lookup
    source_by_type = {}
    for ftype, fname in source_fields:
        normalized = normalize_type(ftype)
        source_by_type.setdefault(normalized, []).append(fname)

    target_by_type = {}
    for ftype, fname in target_fields:
        normalized = normalize_type(ftype)
        target_by_type.setdefault(normalized, []).append(fname)

    # Strategy 1: Positional matching (same index in the field list)
    for i, (s_type, s_name) in enumerate(source_fields):
        if i >= len(target_fields):
            break
        t_type, t_name = target_fields[i]
        if s_name == t_name:
            continue  # Same name, no rename needed
        s_norm = normalize_type(s_type)
        t_norm = normalize_type(t_type)
        # Exact type match, different name
        if s_norm == t_norm:
            renames.append((s_name, t_name))
        # Types differ but names differ too — could be a renamed enum/type at same position
        # Accept if the surrounding fields (i-1, i+1) match by name — strong positional signal
        else:
            neighbors_match = 0
            if i > 0 and i - 1 < len(target_fields):
                if source_fields[i - 1][1] == target_fields[i - 1][1]:
                    neighbors_match += 1
            if i + 1 < len(source_fields) and i + 1 < len(target_fields):
                if source_fields[i + 1][1] == target_fields[i + 1][1]:
                    neighbors_match += 1
            if neighbors_match >= 1:
                renames.append((s_name, t_name))

    # Strategy 2: For unmatched fields, try type+fuzzy name matching
    renamed_source = {r[0] for r in renames}
    renamed_target = {r[1] for r in renames}

    for s_type, s_name in source_fields:
        if s_name in renamed_source:
            continue
        s_norm = normalize_type(s_type)
        # Check if this exact name exists in target — if so, no rename needed
        if any(fname == s_name for _, fname in target_fields):
            continue
        # Look for a target field with same type but different name
        for t_type, t_name in target_fields:
            if t_name in renamed_target:
                continue
            t_norm = normalize_type(t_type)
            if s_norm == t_norm and s_name != t_name:
                # Check the names are similar enough (not totally different fields)
                ratio = SequenceMatcher(None, s_name.lower(), t_name.lower()).ratio()
                if ratio >= 0.4:
                    renames.append((s_name, t_name))
                    renamed_source.add(s_name)
                    renamed_target.add(t_name)
                    break

    return renames


def normalize_type(type_str: str) -> str:
    """Normalize a C# type string for comparison (strip namespaces, whitespace)."""
    # Remove namespace prefixes
    t = re.sub(r'\w+\.', '', type_str)
    # Normalize whitespace
    t = re.sub(r'\s+', '', t)
    return t.lower()


# ---------------------------------------------------------------------------
# Dependency resolution
# ---------------------------------------------------------------------------

def resolve_dependencies(
    root_file: Path,
    guid_index: dict[str, Path],
) -> dict[str, Path]:
    resolved = {}
    visited_files = set()
    queue = [root_file]

    while queue:
        current = queue.pop(0)
        if current in visited_files:
            continue
        visited_files.add(current)

        guids = extract_guids(current)
        for guid in guids:
            if guid in resolved:
                continue

            if guid not in guid_index:
                print(f"  WARNING: GUID {guid} referenced in {current.name} not found in source project")
                continue

            asset_path = guid_index[guid]
            resolved[guid] = asset_path

            ext = asset_path.suffix.lower()
            if ext in PARSEABLE_EXTENSIONS and asset_path.exists():
                queue.append(asset_path)

    return resolved


# ---------------------------------------------------------------------------
# Target project checks
# ---------------------------------------------------------------------------

def check_target_project(
    deps: dict[str, Path],
    target_index: dict[str, Path],
) -> tuple[dict[str, Path], dict[str, Path]]:
    """Check which deps already exist in target project by GUID.
    Unity resolves assets by GUID (not filename), so if the GUID exists
    in the target project the asset is already present regardless of name/size.
    """
    to_copy = {}
    already_present = {}

    for guid, source_path in deps.items():
        if guid not in target_index:
            to_copy[guid] = source_path
        else:
            target_path = target_index[guid]
            already_present[guid] = source_path
            # Warn if the asset exists under a different filename (prior rename)
            if target_path.exists() and target_path.name != source_path.name:
                print(f"  NOTE: {source_path.name} exists in target as "
                      f"'{target_path.name}' (same GUID {guid[:12]}..., different filename)")

    return to_copy, already_present



# ---------------------------------------------------------------------------
# Script auto-matching
# ---------------------------------------------------------------------------

def find_script_dependencies(
    deps: dict[str, Path],
    source_index: dict[str, Path],
) -> dict[str, Path]:
    """Extract only .cs script GUIDs from the full dependency set."""
    scripts = {}
    for guid, path in deps.items():
        if path.suffix.lower() == ".cs":
            scripts[guid] = path
    return scripts


def auto_match_scripts(
    script_deps: dict[str, Path],
    target_class_index: dict[str, tuple[str, Path]],
    manual_swaps: dict[str, str],
) -> tuple[list[tuple[str, str, str]], list[tuple[str, str]], list[tuple[str, Path]]]:
    """
    Auto-match source scripts to target project scripts.

    Returns:
        matched: list of (source_guid, target_guid, match_info_str)
        auto_renames: list of (old_field, new_field) detected across all matched scripts
        unmatched: list of (source_guid, source_path) for scripts with no match
    """
    matched = []
    auto_renames = []
    unmatched = []

    for source_guid, source_path in script_deps.items():
        # Skip if already covered by manual --swap-script
        if source_guid in manual_swaps:
            continue

        source_class = extract_class_name(source_path)
        if source_class is None:
            unmatched.append((source_guid, source_path))
            continue

        result = find_script_match(source_class, target_class_index)
        if result is None:
            unmatched.append((source_guid, source_path))
            continue

        match_type, target_guid, target_path = result
        target_class = extract_class_name(target_path)

        info = f"{source_class} -> {target_class} ({match_type} match)"
        matched.append((source_guid, target_guid, info))

        # Detect field renames between the two scripts
        source_fields = extract_serialized_fields(source_path)
        target_fields = extract_serialized_fields(target_path)
        renames = detect_field_renames(source_fields, target_fields)
        if renames:
            for old_name, new_name in renames:
                auto_renames.append((old_name, new_name))

    return matched, auto_renames, unmatched


# ---------------------------------------------------------------------------
# Filename collision resolution
# ---------------------------------------------------------------------------

def resolve_filename_collisions(
    deps: dict[str, Path],
    root_paths: list[Path],
) -> dict[str, str]:
    """
    Detect filename collisions among assets to be copied (different GUIDs,
    same filename). Returns a guid -> renamed_filename map for colliding assets.
    Root prefabs get priority (keep original name).
    """
    # Track filename -> first guid that claimed it
    claimed = {}  # filename -> guid
    renames = {}  # guid -> new_filename

    # Root prefabs claim their names first
    for p in root_paths:
        claimed[p.name] = "ROOT"

    for guid, asset_path in deps.items():
        name = asset_path.name
        if name not in claimed:
            claimed[name] = guid
        elif claimed[name] != guid:
            # Collision! Two different assets with the same filename
            stem = asset_path.stem
            suffix = asset_path.suffix
            counter = 1
            new_name = f"{stem}_{counter}{suffix}"
            while new_name in claimed:
                counter += 1
                new_name = f"{stem}_{counter}{suffix}"
            claimed[new_name] = guid
            renames[guid] = new_name
            # Find who claimed the original name for the warning
            original_guid = claimed[name]
            original_src = deps.get(original_guid)
            original_info = f" from {original_src}" if original_src else ""
            print(f"  WARNING: FILENAME COLLISION for '{name}':")
            print(f"    Kept:    GUID {original_guid[:12]}...{original_info}")
            print(f"    Renamed: GUID {guid[:12]}... from {asset_path}")
            print(f"    New name: {new_name}")

    return renames


# ---------------------------------------------------------------------------
# File operations
# ---------------------------------------------------------------------------

def copy_asset(
    asset_path: Path,
    target_dir: Path,
    target_name: str = None,
    new_guid: str = None,
    force: bool = False,
) -> bool:
    if not asset_path.exists():
        if asset_path.is_dir():
            return False
        print(f"  SKIP (not found): {asset_path}")
        return False

    final_name = target_name or asset_path.name
    target_file = target_dir / final_name
    meta_source = Path(str(asset_path) + ".meta")
    meta_target = target_dir / (final_name + ".meta")

    if target_file.exists() and not force:
        print(f"  SKIP (exists in target dir): {target_file.name}")
        return False

    shutil.copy2(asset_path, target_file)

    if meta_source.exists():
        if new_guid:
            meta_content = meta_source.read_text(encoding="utf-8", errors="replace")
            meta_content = META_GUID_PATTERN.sub(f"guid: {new_guid}", meta_content, count=1)
            meta_target.write_text(meta_content, encoding="utf-8", newline="\n")
        else:
            shutil.copy2(meta_source, meta_target)

    return True


def apply_guid_remaps(file_path: Path, guid_remap: dict[str, str]):
    content = file_path.read_text(encoding="utf-8", errors="replace")
    for old_guid, new_guid in guid_remap.items():
        content = content.replace(old_guid, new_guid)
    file_path.write_text(content, encoding="utf-8", newline="\n")


def apply_all_swaps_and_renames(
    target_dir: Path,
    deps: dict[str, Path],
    root_names: str | list[str],
    all_swaps: list[tuple[str, str]],
    all_renames: list[tuple[str, str]],
    filename_renames: dict[str, str] = None,
):
    """Apply script GUID swaps and field renames across ALL copied parseable files."""
    if filename_renames is None:
        filename_renames = {}

    # Normalize root_names to a list
    if isinstance(root_names, str):
        root_names = [root_names]

    # Collect all parseable files in the target dir that we copied
    files_to_patch = []

    # Always include the root prefabs/assets
    for name in root_names:
        root_file = target_dir / name
        if root_file.exists() and root_file not in files_to_patch:
            files_to_patch.append(root_file)

    # Include all other parseable copied files (nested prefabs, assets, etc.)
    for guid, asset_path in deps.items():
        ext = asset_path.suffix.lower()
        if ext in PARSEABLE_EXTENSIONS or ext == ".prefab" or ext == ".asset":
            final_name = filename_renames.get(guid, asset_path.name)
            copied_file = target_dir / final_name
            if copied_file.exists() and copied_file not in files_to_patch:
                files_to_patch.append(copied_file)

    # Apply script GUID swaps
    if all_swaps:
        print(f"\nApplying script swaps across {len(files_to_patch)} files...")
        for file_path in files_to_patch:
            content = file_path.read_text(encoding="utf-8", errors="replace")
            changed = False
            for old_guid, new_guid in all_swaps:
                count = content.count(old_guid)
                if count > 0:
                    content = content.replace(old_guid, new_guid)
                    changed = True
                    print(f"  {file_path.name}: swapped {old_guid[:12]}... -> {new_guid[:12]}... ({count}x)")
            if changed:
                file_path.write_text(content, encoding="utf-8", newline="\n")

    # Apply field renames
    if all_renames:
        print(f"\nApplying field renames across {len(files_to_patch)} files...")
        for file_path in files_to_patch:
            content = file_path.read_text(encoding="utf-8", errors="replace")
            changed = False
            for old_name, new_name in all_renames:
                pattern = re.compile(rf'^(\s+){re.escape(old_name)}:', re.MULTILINE)
                matches = pattern.findall(content)
                if matches:
                    content = pattern.sub(rf'\1{new_name}:', content)
                    changed = True
                    print(f"  {file_path.name}: renamed '{old_name}' -> '{new_name}' ({len(matches)}x)")
            if changed:
                file_path.write_text(content, encoding="utf-8", newline="\n")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Export a Unity prefab with all dependencies to another project.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--source-project", required=True,
        help="Root of the source Unity project",
    )
    parser.add_argument(
        "--target-project", default=None,
        help="Root of the target Unity project. If omitted, inferred from --target-dir.",
    )
    parser.add_argument(
        "--prefab", action="append", required=True,
        help="Prefab/asset path relative to source project root. Can be specified multiple times.",
    )
    parser.add_argument(
        "--target-dir", required=True,
        help="Target directory to copy all assets into (flat structure)",
    )
    parser.add_argument(
        "--swap-script", action="append", default=[],
        metavar="OLD_GUID:NEW_GUID",
        help="Manual script GUID swap. Can be specified multiple times.",
    )
    parser.add_argument(
        "--rename-field", action="append", default=[],
        metavar="OLD_NAME:NEW_NAME",
        help="Manual field rename. Can be specified multiple times.",
    )
    parser.add_argument(
        "--no-auto-scripts", action="store_true",
        help="Disable automatic script matching (only use manual --swap-script).",
    )
    parser.add_argument(
        "--force", action="store_true",
        help="Overwrite existing files in target directory",
    )
    parser.add_argument(
        "--dry-run", action="store_true",
        help="Show what would be done without actually doing it",
    )

    args = parser.parse_args()

    source_project = Path(args.source_project)
    target_dir = Path(args.target_dir)

    # Resolve all prefab paths
    prefab_paths = []
    for p in args.prefab:
        rel = p.replace("\\", "/")
        full = source_project / rel
        if not full.exists():
            print(f"ERROR: Prefab not found: {full}")
            sys.exit(1)
        prefab_paths.append(full)

    # Infer target project root
    target_project = None
    if args.target_project:
        target_project = Path(args.target_project)
    else:
        check = target_dir
        while check != check.parent:
            if check.name == "Assets" and (check.parent / "ProjectSettings").exists():
                target_project = check.parent
                break
            check = check.parent
        if target_project is None:
            print("WARNING: Could not infer target project root. "
                  "GUID dedup and auto script matching disabled.")

    # Validate
    if not source_project.exists():
        print(f"ERROR: Source project not found: {source_project}")
        sys.exit(1)

    # Parse manual swaps/renames
    manual_swaps = {}
    for swap in args.swap_script:
        parts = swap.split(":")
        if len(parts) != 2:
            print(f"ERROR: Invalid --swap-script format: {swap}")
            sys.exit(1)
        manual_swaps[parts[0]] = parts[1]

    manual_renames = []
    for rename in args.rename_field:
        parts = rename.split(":")
        if len(parts) != 2:
            print(f"ERROR: Invalid --rename-field format: {rename}")
            sys.exit(1)
        manual_renames.append((parts[0], parts[1]))

    # Build GUID indexes
    source_index = build_guid_index(source_project, "source project")
    target_index = {}
    if target_project:
        target_index = build_guid_index(target_project, "target project")

    # Resolve all dependencies across all prefabs
    deps = {}
    for prefab_path in prefab_paths:
        print(f"\nResolving dependencies for {prefab_path.name}...")
        prefab_deps = resolve_dependencies(prefab_path, source_index)
        deps.update(prefab_deps)

    # -----------------------------------------------------------------------
    # Script auto-matching
    # -----------------------------------------------------------------------
    all_swaps = list(manual_swaps.items())  # (old_guid, new_guid) pairs
    all_renames = list(manual_renames)

    script_deps = find_script_dependencies(deps, source_index)
    if script_deps:
        print(f"\n  Found {len(script_deps)} script references in dependency chain:")
        for guid, path in script_deps.items():
            cls = extract_class_name(path) or "???"
            manual = " (manual swap provided)" if guid in manual_swaps else ""
            print(f"    {cls} ({guid[:12]}...) @ {path.name}{manual}")

    if script_deps and target_project and not args.no_auto_scripts:
        print(f"\nAuto-matching scripts against target project...")
        target_class_index = build_class_index(target_project)
        print(f"  Found {len(target_class_index)} classes in target project")

        matched, auto_renames, unmatched = auto_match_scripts(
            script_deps, target_class_index, manual_swaps,
        )

        if matched:
            print(f"\n  AUTO-MATCHED {len(matched)} scripts:")
            for source_guid, target_guid, info in matched:
                print(f"    {info}")
                all_swaps.append((source_guid, target_guid))

        if auto_renames:
            # Deduplicate with manual renames
            existing_renames = {r[0] for r in all_renames}
            new_renames = [(o, n) for o, n in auto_renames if o not in existing_renames]
            if new_renames:
                print(f"\n  AUTO-DETECTED {len(new_renames)} field renames:")
                for old_name, new_name in new_renames:
                    print(f"    {old_name} -> {new_name}")
                all_renames.extend(new_renames)

        if unmatched:
            print(f"\n  UNMATCHED {len(unmatched)} scripts (no equivalent found in target):")
            for guid, path in unmatched:
                cls = extract_class_name(path) or "???"
                print(f"    WARNING: {cls} ({guid[:12]}...) — will be missing in target!")
                print(f"      Use --swap-script {guid}:<TARGET_GUID> to provide manually")

    # Remove swapped script GUIDs from deps (target already has them)
    swap_old_guids = {s[0] for s in all_swaps}
    deps = {g: p for g, p in deps.items() if g not in swap_old_guids}

    # -----------------------------------------------------------------------
    # Target project GUID dedup
    # -----------------------------------------------------------------------
    if target_index:
        print(f"\nChecking against target project...")
        to_copy, already_present = check_target_project(deps, target_index)

        if already_present:
            print(f"  {len(already_present)} assets already present (will skip):")
            for guid, path in already_present.items():
                print(f"    SKIP: {path.name} @ {target_index[guid]}")

        deps = to_copy

    # -----------------------------------------------------------------------
    # Filename collision resolution
    # -----------------------------------------------------------------------
    print(f"\nChecking for filename collisions...")
    filename_renames = resolve_filename_collisions(deps, prefab_paths)

    # -----------------------------------------------------------------------
    # Summary
    # -----------------------------------------------------------------------
    print(f"\n{'='*60}")
    print(f"EXPORT SUMMARY")
    print(f"{'='*60}")
    print(f"  Dependencies to copy: {len(deps)}")
    print(f"  Script swaps: {len(all_swaps)}")
    print(f"  Field renames: {len(all_renames)}")
    print(f"  Filename renames: {len(filename_renames)}")

    if deps:
        print(f"\n  Files to copy:")
        for guid, path in sorted(deps.items(), key=lambda x: x[1].suffix):
            rename_info = f" -> {filename_renames[guid]}" if guid in filename_renames else ""
            print(f"    [{path.suffix:15s}] {path.name}{rename_info}")

    if args.dry_run:
        print(f"\n--- DRY RUN: No files were copied ---")
        return

    # -----------------------------------------------------------------------
    # Execute
    # -----------------------------------------------------------------------
    target_dir.mkdir(parents=True, exist_ok=True)

    # Copy root prefabs
    print(f"\nCopying {len(prefab_paths)} root prefab(s)...")
    for prefab_path in prefab_paths:
        copy_asset(prefab_path, target_dir, force=args.force)

    # Copy dependencies
    print(f"Copying {len(deps)} dependencies...")
    copied = 0
    for guid, asset_path in deps.items():
        target_name = filename_renames.get(guid)
        if copy_asset(asset_path, target_dir, target_name=target_name,
                      force=args.force):
            copied += 1
    print(f"  Copied {copied} files")

    # Apply script swaps + field renames across ALL copied files
    # Collect all root prefab names for patching
    all_root_names = [p.name for p in prefab_paths]
    apply_all_swaps_and_renames(
        target_dir, deps, all_root_names, all_swaps, all_renames,
        filename_renames,
    )

    # -----------------------------------------------------------------------
    # Post-export verification: check for broken references
    # -----------------------------------------------------------------------
    if target_index:
        print(f"\nVerifying references...")
        # Rebuild target index to include newly copied files
        updated_target_index = build_guid_index(
            target_project, "target project (post-export verification)")

        # Scan all copied parseable files for GUID references
        warnings = []
        files_to_check = [target_dir / p.name for p in prefab_paths]
        for guid, asset_path in deps.items():
            final_name = filename_renames.get(guid, asset_path.name)
            f = target_dir / final_name
            if f.exists() and f.suffix.lower() in PARSEABLE_EXTENSIONS:
                files_to_check.append(f)

        for f in files_to_check:
            if not f.exists():
                continue
            referenced_guids = extract_guids(f)
            for ref_guid in referenced_guids:
                if is_builtin_guid(ref_guid):
                    continue
                if ref_guid not in updated_target_index:
                    # Might be a package asset — check source
                    if ref_guid in source_index:
                        src = source_index[ref_guid]
                        warnings.append(
                            f"  WARNING: {f.name} references GUID {ref_guid[:12]}... "
                            f"({src.name}) — NOT FOUND in target project. "
                            f"May be a package asset or missing dependency.")
                    else:
                        warnings.append(
                            f"  WARNING: {f.name} references GUID {ref_guid[:12]}... "
                            f"— not found in source or target project.")

        if warnings:
            # Deduplicate
            unique_warnings = list(dict.fromkeys(warnings))
            print(f"\n  {len(unique_warnings)} potential missing references:")
            for w in unique_warnings:
                print(w)
        else:
            print(f"  All references resolved OK.")

    print(f"\n{'='*60}")
    print(f"Done! Assets exported to {target_dir}")
    if filename_renames:
        print(f"  {len(filename_renames)} files were renamed to avoid filename collisions.")
    print(f"Open Unity and let it import the new assets.")


if __name__ == "__main__":
    main()
