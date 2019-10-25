<!DOCTYPE html>
<html>
<meta charset="UTF-8">
<head>
<style type='text/css'>
html { overflow:auto; }
body { font-size:11pt; font:Arial; }
td { vertical-align:center; }
</style>
</head>
<body>
<table width='100%'><tr>
<td width='40'><img src='[[IMG:Logo-128x128.png]]' alt='Logo' width='32' height='32'/></td>
<td>
<b>Satisfactory Savegame Tool</b> - v0.3 alpha<br/>
<b>&copy;2019 SillyBits</b>
</td>
</tr></table>
<br/>
<br/>
<br/>
<b>Changelog</b><br/>
<br/>

<p><u>v0.3 alpha</u><ul>
	<li>New: Compare action to allow for showing differences between two savegames.</li>
	<li>Bugfix: Fixed picking an unsupported language id on first start which caused load dialog to crash (#6).</li>
	<li>Bugfix: Added types missing: TrainSimulationData (#5), ProjectileData &amp; Guid.</li>
	<li>Bugfix: Fixed missing 'Data.bin' with incident reports on cloud saves (#4).</li>
	<li>Change: Fallback added in case error reporting dialog can't be shown.</li>
	<li>Change: Enabled buildings tree to show buildings coming from mods.</li>
</ul></p>

<p><u>v0.2 alpha</u><ul>
	<li>Support for savegames up to current v0.2.1.17 (Build 106504).</li>
	<li>Bugfix: Fixed crash when editing within one of those specialized viewers (#2).</li>
	<li>Bugfix: Extended 'ArrayProperty' to also handle inner types 'EnumProperty' and 'StrProperty' (#3).</li>
	<li>Bugfix: Fixed missing 'Data.bin' with incident reports on cloud saves (#4).</li>
	<li>Bugfix: Added type missing: TimeTableStop.</li>
	<li>Bugfix: Added visualizers missing: InventoryStack, InventoryItem, TimeTableStop &amp; RailroadTrackPosition.</li>
	<li>Change: Enabled specialized trees to show modification state.</li>
	<li>Change: Unknown inner types with 'ArrayProperty' will raise an 'UnknownPropertyException' instead.</li>
</ul></p>

<p><u>v0.1 alpha</u><ul>
	<li>Full port of "Satisfactory Savegame Repairer".</li>
</ul></p>

</body>
</html>