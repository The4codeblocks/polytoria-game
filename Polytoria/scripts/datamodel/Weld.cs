// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using Godot;
using Polytoria.Attributes;

namespace Polytoria.Datamodel;

[Instantiable]
public partial class Weld : Instance
{
	Instance? _part0;
	Instance? _part1;

	[Editable, ScriptProperty]
	public Instance? Part0
	{
		get => _part0;
		set
		{
			if (_part0 == value) return;
			if (value != null && value == _part1) return;
			_part0 = value;
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty]
	public Instance? Part1
	{
		get => _part1;
		set
		{
			if (_part1 == value) return;
			if (value != null && value == _part0) return;
			_part1 = value;
			OnPropertyChanged();
		}
	}

	[ScriptMethod]
	public void Break()
	{
		Part0 = null;
		Part1 = null;
	}

	public override void EnterTree()
	{
		base.EnterTree();

		if (_part0 == null && Parent is Physical)
		{
			Part0 = Parent;
		}
	}
}
