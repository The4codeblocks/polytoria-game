using System.Collections.Generic;
using Polytoria.Datamodel;
using Godot;

namespace Polytoria.Physics;

public static class WeldAssemblyManager
{
	private static readonly Dictionary<Part, WeldAssembly> _assemblies = new(ReferenceEqualityComparer.Instance);
	private static readonly Dictionary<Weld, (Part? part0, Part? part1)> _welds = new(ReferenceEqualityComparer.Instance);
	private static readonly HashSet<Part> _dirtyParts = new(ReferenceEqualityComparer.Instance);
	private static readonly HashSet<WeldAssembly> _dirtyAssemblies = new(ReferenceEqualityComparer.Instance);

	private static bool _buildQueued;
	private static bool _building;
	private static int _bulkEditDepth;

	internal static void OnWeldChanged(Weld weld, Part? old0, Part? old1, Part? new0, Part? new1)
	{
		if (old0 != null && old1 != null)
		{
			OnWeldRemoved(weld, old0, old1);
		}

		if (new0 != null && new1 != null && new0 != new1)
		{
			OnWeldAdded(weld, new0, new1);
		}
	}

	internal static void OnWeldAdded(Weld weld, Part a, Part b)
	{
		WeldGraph.Add(weld, a, b);
		_welds[weld] = (a, b);

		WeldAssembly? assA = GetAssembly(a);
		WeldAssembly? assB = GetAssembly(b);

		if (assA != null && assA == assB)
		{
			return;
		}

		QueueBuild(a);
		QueueBuild(b);
	}

	private static void QueueBuild(Part part)
	{
		if (part.IsDeleted || part.IsInTemporary)
		{
			return;
		}

		_dirtyParts.Add(part);

		if (_buildQueued)
		{
			return;
		}

		_buildQueued = true;

		Callable.From(() =>
		{
			_buildQueued = false;
			FlushBuildQueue();
		}).CallDeferred();
	}

	private static void FlushBuildQueue()
	{
		if (_building)
		{
			return;
		}

		if (_dirtyParts.Count == 0)
		{
			return;
		}

		_building = true;

		try
		{
			HashSet<Part> dirty = new(_dirtyParts, ReferenceEqualityComparer.Instance);
			_dirtyParts.Clear();

			HashSet<Part> visited = new(ReferenceEqualityComparer.Instance);

			foreach (Part start in dirty)
			{
				if (visited.Contains(start))
				{
					continue;
				}

				if (start.IsDeleted || start.IsInTemporary)
				{
					continue;
				}

				HashSet<Part> component = WeldGraph.GetComponent(start);

				foreach (Part part in component)
				{
					visited.Add(part);
				}

				if (component.Count <= 1)
				{
					continue;
				}

				HashSet<WeldAssembly> oldAssemblies = new(ReferenceEqualityComparer.Instance);
				Part? preferredRoot = null;

				foreach (Part part in component)
				{
					if (_assemblies.TryGetValue(part, out WeldAssembly? oldAssembly))
					{
						if (oldAssemblies.Add(oldAssembly))
						{
							if (preferredRoot == null || oldAssembly.Root.Anchored)
							{
								preferredRoot = oldAssembly.Root;
							}
						}
					}
				}

				foreach (WeldAssembly oldAssembly in oldAssemblies)
				{
					HashSet<Part> oldParts = new(oldAssembly.Parts, ReferenceEqualityComparer.Instance);
					oldAssembly.Destroy();
					Unregister(oldParts);
				}

				Build(component, preferredRoot ?? start);
			}
		}
		finally
		{
			_building = false;
		}
	}

	internal static void BreakWelds(IEnumerable<Weld> welds)
	{
		BeginBulkEdit();

		try
		{
			foreach (Weld weld in welds)
			{
				if (!weld.IsDeleted)
				{
					weld.Break();
				}
			}
		}
		finally
		{
			EndBulkEdit();
		}
	}

	internal static void BeginBulkEdit()
	{
		if (_bulkEditDepth == 0)
		{
			FlushBuildQueue();
		}

		_bulkEditDepth++;
	}

	internal static void EndBulkEdit()
	{
		if (_bulkEditDepth <= 0)
		{
			return;
		}

		_bulkEditDepth--;

		if (_bulkEditDepth == 0)
		{
			FlushSplitQueue();
			FlushBuildQueue();
		}
	}

	internal static void OnWeldRemoved(Weld weld)
	{
		if (_welds.TryGetValue(weld, out var tuple))
		{
			OnWeldRemoved(weld, tuple.part0, tuple.part1);
		}
	}

	internal static void OnWeldRemoved(Weld weld, Part? a, Part? b)
	{
		if (_bulkEditDepth == 0)
		{
			FlushBuildQueue();
		}

		WeldAssembly? old = null;

		if (a != null)
		{
			old = GetAssembly(a);
		}

		if (old == null && b != null)
		{
			old = GetAssembly(b);
		}

		WeldGraph.Remove(weld, a, b);
		_welds.Remove(weld);

		if (a == null || b == null || old == null)
		{
			return;
		}

		if (_bulkEditDepth > 0)
		{
			_dirtyAssemblies.Add(old);
			return;
		}

		RebuildDirtyAssembly(old);
	}

	private static void FlushSplitQueue()
	{
		if (_dirtyAssemblies.Count == 0)
		{
			return;
		}

		HashSet<WeldAssembly> dirty = new(_dirtyAssemblies, ReferenceEqualityComparer.Instance);
		_dirtyAssemblies.Clear();

		foreach (WeldAssembly old in dirty)
		{
			RebuildDirtyAssembly(old);
		}
	}

	private static void RebuildDirtyAssembly(WeldAssembly old)
	{
		if (old.Parts.Count == 0)
			return;

		HashSet<Part> oldParts = new(old.Parts, ReferenceEqualityComparer.Instance);
		HashSet<Part> validParts = new(ReferenceEqualityComparer.Instance);

		foreach (Part part in oldParts)
		{
			if (!part.IsDeleted && !part.IsInTemporary)
				validParts.Add(part);
		}

		if (validParts.Count == 0)
		{
			old.Destroy();
			Unregister(oldParts);
			return;
		}

		List<HashSet<Part>> components = [];
		HashSet<Part> unvisited = new(validParts, ReferenceEqualityComparer.Instance);

		while (unvisited.Count > 0)
		{
			Part start = default!;

			foreach (Part part in unvisited)
			{
				start = part;
				break;
			}

			HashSet<Part> component = WeldGraph.GetComponentWithin(start, validParts);

			foreach (Part part in component)
				unvisited.Remove(part);

			if (component.Count > 0)
				components.Add(component);
		}

		if (components.Count == 1 && components[0].Count == oldParts.Count)
		{
			return;
		}

		HashSet<Part>? retained = null;

		foreach (HashSet<Part> component in components)
		{
			if (component.Contains(old.Root))
			{
				retained = component;
				break;
			}
		}

		if (retained == null)
		{
			old.Destroy();
			Unregister(oldParts);

			foreach (HashSet<Part> component in components)
				Build(component, null);

			return;
		}

		List<HashSet<Part>> separatedComponents = [];

		foreach (HashSet<Part> component in components)
		{
			if (!ReferenceEquals(component, retained))
				separatedComponents.Add(component);
		}

		foreach (Part part in oldParts)
		{
			if (retained.Contains(part))
				continue;

			_assemblies.Remove(part);
			old.Parts.Remove(part);
			old.LocalTransforms.Remove(part);

			if (old.Physicalized && part.Assembly == old)
				part.DetachFromAssembly();
		}

		RefreshAssemblyMetadata(old);

		foreach (HashSet<Part> component in separatedComponents)
		{
			if (component.Count <= 1)
				continue;

			Build(component, null);
		}
	}

	private static void RefreshAssemblyMetadata(WeldAssembly assembly)
	{
		float totalMass = 0;
		bool anchored = false;

		foreach (Part part in assembly.Parts)
		{
			anchored |= part.Anchored;
			totalMass += Mathf.Max(part.Mass, Physical.MinMass);
		}

		assembly.Anchored = anchored;

		if (assembly.Physicalized)
		{
			assembly.Root.GDRigidBody.Mass = totalMass;

			foreach (Part part in assembly.Parts)
				part.UpdateFreeze();
		}
	}

	internal static void OnPartDeleted(Part part)
	{
		if (_bulkEditDepth == 0)
		{
			FlushBuildQueue();
		}

		WeldAssembly? old = GetAssembly(part);

		foreach (Weld weld in WeldGraph.GetWelds(part).ToArray())
		{
			Part? other = WeldGraph.GetOtherPart(weld, part);
			WeldGraph.Remove(weld, part, other);
			_welds.Remove(weld);
		}

		if (old == null)
		{
			return;
		}

		if (_bulkEditDepth > 0)
		{
			_dirtyAssemblies.Add(old);
			return;
		}

		RebuildDirtyAssembly(old);
	}

	private static WeldAssembly? GetAssembly(Part part)
	{
		if (_assemblies.TryGetValue(part, out WeldAssembly? assembly))
		{
			return assembly;
		}

		return null;
	}

	private static void Register(WeldAssembly assembly)
	{
		foreach (Part part in assembly.Parts)
		{
			_assemblies[part] = assembly;
		}
	}

	private static void Unregister(HashSet<Part> parts)
	{
		foreach (Part part in parts)
		{
			_assemblies.Remove(part);
		}
	}

	private static void Build(HashSet<Part> parts, Part? preferredRoot)
	{
		if (parts.Count <= 1)
		{
			foreach (Part part in parts)
			{
				_assemblies.Remove(part);
				part.DetachFromAssembly();
			}

			return;
		}

		WeldAssembly assembly = WeldAssembly.Build(parts, preferredRoot);
		Register(assembly);
	}
}