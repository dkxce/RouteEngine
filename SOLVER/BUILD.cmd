SET MSBE=C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\MSBuild.exe
%MSBE% %CD%\XMLSaved\XMLSaved.csproj
%MSBE% %CD%\dkxce.Route.Classes\dkxce.Route.Classes.csproj
%MSBE% %CD%\dkxce.Route.GSolver\dkxce.Route.GSolver.csproj
%MSBE% %CD%\dkxce.Route.WayList\dkxce.Route.WayList.csproj
%MSBE% %CD%\dkxce.Route.ISolver\dkxce.Route.ISolver.csproj
%MSBE% %CD%\dkxce.Route.Matrix\dkxce.Route.Matrix.csproj
%MSBE% %CD%\dkxce.Route.PointNLL\dkxce.Route.PointNLL.csproj
%MSBE% %CD%\dkxce.Route.Regions\dkxce.Route.Regions.csproj
%MSBE% %CD%\dkxce.Route.Shp2Rt\dkxce.Route.Shp2Rt.csproj
%MSBE% %CD%\dkxce.Route.ServiceSolver\dkxce.Route.ServiceSolver.csproj
%MSBE% %CD%\SSKeySys\SSKeySys.csproj
%MSBE% %CD%\SSProtector\SSProtector.csproj
%MSBE% %CD%\Syslib\Syslib.csproj /p:Configuration=Debug /p:Platform=x86
%MSBE% %CD%\nmsRoutesDirectCall\nmsRoutesDirectCall.csproj
%MSBE% %CD%\nmsRoutesWebTest\nmsRoutesWebTest.csproj
%MSBE% %CD%\RGWay2RTE\RGWay2RTE.csproj
%MSBE% %CD%\RouteGraphBatcher\RouteGraphBatcher.csproj
%MSBE% %CD%\RouteGraphCalcMatrix\RouteGraphCalcMatrix.csproj
%MSBE% %CD%\RouteGraphCalcRG\RouteGraphCalcRG.csproj
%MSBE% %CD%\RouteGraphCreator\RouteGraphCreator.csproj
%MSBE% %CD%\RGSolver\RouteGraphSolver.csproj
%MSBE% %CD%\TEST_MAP\RouteMapTest.csproj
%MSBE% %CD%\WorkingLoadTest\WorkingLoadTest.csproj
%MSBE% %CD%\RouteServiceState\RouteServiceState.csproj
%MSBE% %CD%\ShapesBBox2Regions\ShapesBBox2Regions.csproj
%MSBE% %CD%\ShapesMerger\ShapesMerger.csproj
%MSBE% %CD%\ShapesPolygonsExtractor\ShapesPolygonsExtractor.csproj
%MSBE% %CD%\RoutesKeyGen\RoutesKeyGen.csproj
cd ..\READY\MAIN_DLLs
call delete_unused_files.cmd
cd ..\MapCreator
call delete_unused_files.cmd
cd ..\Service
call delete_unused_files.cmd
cd ..\TEST
call delete_unused_files.cmd
cd ..\TOOLS
call delete_unused_files.cmd
cd ..\..\SOLVER
pause