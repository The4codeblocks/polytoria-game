// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Polytoria.Attributes;
using Polytoria.Shared;

namespace Polytoria.Datamodel.Resources;

[Instantiable, SaveIgnore]
public partial class TextAsset : DataAsset
{
	private string _text = "";

	public override byte[]? Data => System.Text.Encoding.UTF8.GetBytes(_text);

	[Editable, ScriptProperty]
	public string Text
	{
		get => _text;
		set
		{
			_text = value;
		}
	}
}
