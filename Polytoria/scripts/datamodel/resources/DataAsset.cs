// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Polytoria.Datamodel.Resources;

/// <summary>
/// Base class for assets that link to text or binary data
/// </summary>
[Abstract]
public partial class DataAsset : BaseAsset
{
	public virtual byte[]? Data => null;
}
