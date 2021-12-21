/**
* @preserve Maintained by the Office of Web & New Media, Missouri State University, web@missouristate.edu
*
* Usage:
* var Overlay = new missouristate.web.TileOverlay(
*    //function to return the full URL of a tile given a set of tile coordinates and a zoom level.
*    function(x, y, z) { return "http://search.missouristate.edu/map/tilesets/baselayer/" + z + "_" + x + "_" + y + ".png"; },
*    //options with which to initialize the tile layer
*    {
*       'map': map, // optional. google.maps.Map reference.
*       'visible': true, //optional. boolean. controls initial display of the layer.
*       'minZoom': 1, // optional. minimum zoom level at which the tile layer will display.
*       'maxZoom': 19, //optional. maximum zoom level at which the tile layer will display.
*       'bounds': LayerBounds, // optional, but strongly encouraged. google.maps.LatLngBounds containing all of the tiles.
*       'percentOpacity': 100,  //optional. If present, tiles will be rendered with a percent opacity other than 100.
*       'mapTypes': [google.maps.MapTypeId.ROADMAP, google.maps.MapTypeId.HYBRID] //optional. If present, tiles will only be drawn when the map type matches
*    });
*/

// Setup the namespace
var missouristate = missouristate || {};
missouristate.web = missouristate.web || {};

missouristate.web.constants = missouristate.web.constants || {};
/** @const */
missouristate.web.constants.TILE_SIZE = 256;
/** @const */
missouristate.web.constants.TILE_INITIAL_RESOLUTION = 2 * Math.PI * 6378137 / missouristate.web.constants.TILE_SIZE;
/** @const */
missouristate.web.constants.TILE_ORIGIN_SHIFT = 2 * Math.PI * 6378137 / 2.0;

/**
* @const
* @type {boolean}
*/
missouristate.web.constants.IE6 = /MSIE 6/i.test(navigator.userAgent);

/**
* @const
* @type {String}
*/
missouristate.web.constants.TILE_CSS_TEXT = /webkit/i.test(navigator.userAgent) ? "-webkit-user-select:none;" : (/Gecko[\/]/i.test(navigator.userAgent) ? "-moz-user-select:none;" : "");

/**
* @constructor
*/
missouristate.web.TileEvents = function() {
    /** @type {Object.<string, missouristate.web.TileOverlay>} */
    this.overlays = {};
    /** @type {google.maps.MapEventListener} */
    this.zoomListener = null;
    /** @type {google.maps.MapEventListener} */
    this.idleListener = null,
    /** @type {google.maps.LatLngBounds} */
        this.viewportBounds = null;
    /** @type {Array.<google.maps.Point>} */
    this.viewportTileBounds = [null, null];
    /** @type {Array.<google.maps.Point>} */
    this.viewportPixelBounds = [null, null];
    /** @type {Array.<google.maps.Point>} */
    this.tileCoords = [null, null];
    this.overlayIndex = 0;
}
/**
* @param {missouristate.web.TileOverlay} newTileOverlay
* returns {string}
*/
missouristate.web.TileEvents.prototype.addOverlay = function(newTileOverlay) {
    var thisIndex = "_" + this.overlayIndex;
    this.overlayIndex++;
    this.overlays[thisIndex] = newTileOverlay;
    if (!this.idleListener)
        google.maps.event.addListener(newTileOverlay.map, 'idle', /** @this {google.maps.Map} */function() {
            this.missouristate$web$tileEvents.viewportPixelBounds = [null, null];

            var z = this.getZoom();

            //Calculate the boundaries for the tiles which overlap the viewport
            var viewportBounds = this.getBounds();
            var viewportTileCoordsNorthEast = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(viewportBounds.getNorthEast(), z);
            var viewportTileCoordsSouthWest = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(viewportBounds.getSouthWest(), z);
            viewportBounds = new google.maps.LatLngBounds(missouristate.web.TileOverlay.fromTileCoordinatesToLatLng(new google.maps.Point(viewportTileCoordsSouthWest.x, viewportTileCoordsSouthWest.y), z),
                    missouristate.web.TileOverlay.fromTileCoordinatesToLatLng(new google.maps.Point(viewportTileCoordsNorthEast.x, viewportTileCoordsNorthEast.y), z));
            this.missouristate$web$tileEvents.viewportBounds = viewportBounds;
            this.missouristate$web$tileEvents.tileCoords = [viewportTileCoordsSouthWest, viewportTileCoordsNorthEast];

            for (var overlay in this.missouristate$web$tileEvents['overlays']) {
                if (overlay) {
                    var proj = this.missouristate$web$tileEvents.overlays[overlay].getProjection();
                    var viewportNorthEastPixel = proj.fromLatLngToDivPixel(viewportBounds.getNorthEast());
                    var viewportSouthWestPixel = proj.fromLatLngToDivPixel(viewportBounds.getSouthWest());
                    this.missouristate$web$tileEvents.viewportPixelBounds = [viewportSouthWestPixel, viewportNorthEastPixel];
                }
            }

            for (var overlay in this.missouristate$web$tileEvents['overlays']) {
                if (overlay)
                    this.missouristate$web$tileEvents.overlays[overlay].draw();
            }
        });

    if (!this.zoomListener)
        google.maps.event.addListener(newTileOverlay.map, 'zoom_changed', /** @this {google.maps.Map} */function() {
            for (var overlay in this.missouristate$web$tileEvents.overlays) {
                if (overlay)
                    this.missouristate$web$tileEvents.overlays[overlay].removeAllTiles();
            }
        });
    return thisIndex;
}

/**
* @param {string} index
*/
missouristate.web.TileEvents.prototype.removeOverlay = function(index) {
    if (this.overlays[index])
        delete this.overlays[index];
    var hasOverlays = false;
    for (var overlay in this.overlays) {
        if (overlay) {
            hasOverlays = true;
            break;
        }
    }
    if (!hasOverlays) {
        if (this.zoomListener)
            google.maps.event.removeListener(this.zoomListener);
        if (this.idleListener)
            google.maps.event.removeListener(this.idleListener);

        this.idleListener = null;
        this.zoomListener = null;
    }
}

/**
* @constructor
* @extends {google.maps.OverlayView}
* @param {function(number, number, number): string} GetTileUrl - function that takes 3 params (x, y, z) and returns a full URL to the tile
* @param {?Object} TileOverlayOptions
*/
missouristate.web.TileOverlay = function(GetTileUrl, TileOverlayOptions) {
    this.getTileUrl = GetTileUrl;

    /** @type {google.maps.Map} */
    this.map = null;
    this.visible = true;
    /** @type {Array.<google.maps.MapTypeId>} */
    this.mapTypes = null;

    if (TileOverlayOptions) {
        TileOverlayOptions['map'] && (this.map = TileOverlayOptions['map']);
        TileOverlayOptions['visible'] && (this.visible = TileOverlayOptions['visible']);
        TileOverlayOptions['mapTypes'] && (this.mapTypes = TileOverlayOptions['mapTypes']);
    }
    this.div_ = null;
    /**
    * @const
    * @type {google.maps.LatLngBounds}
    */
    this.BOUNDS = (TileOverlayOptions && TileOverlayOptions['bounds']) ? TileOverlayOptions['bounds'] : null;

    /** @const */
    this.MIN_ZOOM = (TileOverlayOptions && TileOverlayOptions['minZoom']) ? TileOverlayOptions['minZoom'] : 1;

    /** @const */
    this.MAX_ZOOM = (TileOverlayOptions && TileOverlayOptions['maxZoom']) ? TileOverlayOptions['maxZoom'] : 19;

    /** @const */
    this.PERCENTOPACITY =  (TileOverlayOptions && TileOverlayOptions['percentOpacity']) ? TileOverlayOptions['percentOpacity'] : 100;

    //For the current zoom level, keep track of which tiles have already been drawn
    /**
    * @type {Array.<Array.<Element>>}
    */
    this.tilesDrawn = [];

    /** @type {google.maps.MapEventListener}  */
    this.zoomChangedMapEventListener = null;
    /** @type {google.maps.MapEventListener} */
    this.boundsChangedMapEventListener = null;

    /** @type {google.maps.LatLngBounds} */
    this.drawnBounds = null;

    this.map && this.setMap(this.map);
}
/*
* Closure compiler bug - does not recognize direct assignments to prototype as inheritance. Wrap it in a function call to work around.
*/
function inherit(SubClass, SuperClass) {
    SubClass.prototype = new SuperClass();
}
inherit(missouristate.web.TileOverlay, google.maps.OverlayView);

/**
* @param {google.maps.LatLng} latLng
* @param {number} zoom
* @returns {google.maps.Point}
*/
missouristate.web.TileOverlay.fromLatLngToTileCoordinates = function(latLng, zoom) {
    //LatLng to Meters
    var mx = latLng.lng() * missouristate.web.constants.TILE_ORIGIN_SHIFT / 180.0;
    var my = (Math.log(Math.tan((90 + latLng.lat()) * Math.PI / 360.0)) / (Math.PI / 180.0)) * missouristate.web.constants.TILE_ORIGIN_SHIFT / 180.0;

    //Meters to Pixels
    var res = missouristate.web.constants.TILE_INITIAL_RESOLUTION / Math.pow(2, zoom);
    var px = (mx + missouristate.web.constants.TILE_ORIGIN_SHIFT) / res;
    var py = (my + missouristate.web.constants.TILE_ORIGIN_SHIFT) / res;

    //Pixels to Tile Coords
    var tx = Math.floor(Math.ceil(px / missouristate.web.constants.TILE_SIZE) - 1);
    var ty = Math.pow(2, zoom) - 1 - Math.floor(Math.ceil(py / missouristate.web.constants.TILE_SIZE) - 1);

    return new google.maps.Point(tx, ty);
}

/**
* @param {google.maps.Point} coords
* @param {number} zoom
* @returns {google.maps.LatLng}
*/
missouristate.web.TileOverlay.fromTileCoordinatesToLatLng = function(coords, zoom) {
    //Tile Coords to Meters
    var res = missouristate.web.constants.TILE_INITIAL_RESOLUTION / Math.pow(2, zoom);
    var mx = (coords.x * missouristate.web.constants.TILE_SIZE) * res - missouristate.web.constants.TILE_ORIGIN_SHIFT;
    var my = ((Math.pow(2, zoom) - coords.y) * missouristate.web.constants.TILE_SIZE) * res - missouristate.web.constants.TILE_ORIGIN_SHIFT;

    //Meters to LatLng
    var lng = (mx / missouristate.web.constants.TILE_ORIGIN_SHIFT) * 180.0;
    var lat = 180 / Math.PI * (2 * Math.atan(Math.exp(((my / missouristate.web.constants.TILE_ORIGIN_SHIFT) * 180.0) * Math.PI / 180.0)) - Math.PI / 2.0);

    return new google.maps.LatLng(lat, lng);
}
///////////////////////////////////////////////////////////////////////////////////

/**
* Use strings for the protype name to export the symbol
* @override
* @this {missouristate.web.TileOverlay}
*/
missouristate.web.TileOverlay.prototype['onAdd'] = function() {
    this.div_ = document.createElement("DIV");
    this.div_.style.position = "relative";
    if (!this.visible)
        this.div_.style.display = "none";
    this.setOpacity(this.PERCENTOPACITY) ;
    this.getPanes().mapPane.appendChild(this.div_);
}

/*
* Cleanup drawn tiles
*/
missouristate.web.TileOverlay.prototype.removeAllTiles = function() {
    if (this.div_ == null)
        return;

    while (this.div_.childNodes.length > 0)
        this.div_.removeChild(this.div_.childNodes[0]);

    this.tilesDrawn = [];
    this.drawnBounds = null;
}

/**
* Use strings for the protype name to export the symbol
* @override
* @this {missouristate.web.TileOverlay}
*/
missouristate.web.TileOverlay.prototype['draw'] = function() {
    var mapTypeId = this.map.getMapTypeId();
    if (this.mapTypes) {
        var matchingType = false;
        for (var i = 0; i < this.mapTypes.length; i++) {
            if (this.mapTypes[i] == mapTypeId) {
                matchingType = true;
                break;
            }
        }
        if (!matchingType) {
            this.removeAllTiles();
            return;
        }
    }

    var z = this.map.getZoom();


    //Calculate the boundaries for the tiles which overlap the viewport
    var viewportBounds = this.map.missouristate$web$tileEvents['viewportBounds'];
    if (!viewportBounds)
        return;

    var viewportTileCoordsNorthEast = this.map.missouristate$web$tileEvents['tileCoords'][1];
    var viewportTileCoordsSouthWest = this.map.missouristate$web$tileEvents['tileCoords'][0];

    //Calculate the boundaries for the tiles at this zoom level
    var TileCoordsNorthEast = viewportTileCoordsNorthEast;
    var TileCoordsSouthWest = viewportTileCoordsSouthWest;

    if (this.BOUNDS) {
        TileCoordsNorthEast = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(this.BOUNDS.getNorthEast(), z);
        TileCoordsSouthWest = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(this.BOUNDS.getSouthWest(), z);
    }
    var TileLatLngBoundsForZoom = new google.maps.LatLngBounds(missouristate.web.TileOverlay.fromTileCoordinatesToLatLng(new google.maps.Point(TileCoordsSouthWest.x, TileCoordsSouthWest.y), z),
		missouristate.web.TileOverlay.fromTileCoordinatesToLatLng(new google.maps.Point(TileCoordsNorthEast.x, TileCoordsNorthEast.y), z));


    //Check to see if there are any tiles defined for this zoom level and if they fall within the viewport
    if (z < this.MIN_ZOOM || z > this.MAX_ZOOM || !viewportBounds.intersects(TileLatLngBoundsForZoom))
        return this.removeAllTiles();

    //If tiles previously drawn are now all out of the viewport, start over
    if (this.drawnBounds && !viewportBounds.intersects(this.drawnBounds))
        this.removeAllTiles();
    //Some of the tiles are still displayed. Loop through all the previously drawn tiles and remove those no longer within the viewport
    else if (this.drawnBounds) {
        var drawnNorthEast = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(viewportBounds.getNorthEast(), z);
        var drawnSouthWest = missouristate.web.TileOverlay.fromLatLngToTileCoordinates(viewportBounds.getSouthWest(), z);

        for (var x = drawnNorthEast.x; x <= drawnSouthWest.x; x++)
            for (var y = drawnSouthWest.y; y <= drawnNorthEast.y; y++)
            if (x < viewportTileCoordsNorthEast.x || x > viewportTileCoordsSouthWest.x || y < viewportTileCoordsSouthWest.y || y > viewportTileCoordsNorthEast.y) {
            this.div_.removeChild(this.tilesDrawn["_" + x]["_" + y]);
            delete this.tilesDrawn["_" + x]["_" + y];
        }
    }
    this.drawnBounds = viewportBounds;

    var viewportNorthEastPixel = this.map.missouristate$web$tileEvents['viewportPixelBounds'][1];
    var viewportSouthWestPixel = this.map.missouristate$web$tileEvents['viewportPixelBounds'][0];

    //Loop through all of the possible viewport tiles and see if we need to draw new tiles
    for (var x = viewportTileCoordsSouthWest.x; x <= viewportTileCoordsNorthEast.x; x++) {
        for (var y = viewportTileCoordsNorthEast.y; y <= viewportTileCoordsSouthWest.y; y++) {
            //Check to see if this is a valid tile for this overlay, and that we haven't already drawn it.
            if (x >= TileCoordsSouthWest.x && x <= TileCoordsNorthEast.x && y >= TileCoordsNorthEast.y && y <= TileCoordsSouthWest.y && (!this.tilesDrawn["_" + x] || !this.tilesDrawn["_" + x]["_" + y])) {
                var img = document.createElement("IMG");
                img.style.cssText = "position:absolute;left:" + (viewportSouthWestPixel.x + ((x - viewportTileCoordsSouthWest.x) * missouristate.web.constants.TILE_SIZE)) + "px;top:" + (viewportNorthEastPixel.y + ((y - viewportTileCoordsNorthEast.y) * missouristate.web.constants.TILE_SIZE)) + "px;" + missouristate.web.constants.TILE_CSS_TEXT;
                if (missouristate.web.constants.IE6) {
                    img.style.cssText += "filter:progid:DXImageTransform.Microsoft.AlphaImageLoader(src='" + this.getTileUrl(x, y, z) + "', sizingMethod='scale');";
                    img.src = "http://www.missouristate.edu/images/spacer.gif";
                }
                else
                    img.src = this.getTileUrl(x, y, z);
                img.alt = "";
                img.width = missouristate.web.constants.TILE_SIZE;
                img.height = missouristate.web.constants.TILE_SIZE;
                this.div_.appendChild(img);

                this.tilesDrawn["_" + x] = this.tilesDrawn["_" + x] || [];
                this.tilesDrawn["_" + x]["_" + y] = img;
            }
        }
    }
}

/**
* @override
* @this {missouristate.web.TileOverlay}
*/
missouristate.web.TileOverlay.prototype['setMap'] = function(Map) {
    //If we are removing the overlay from the map, first remove the div from the map pane. After the call to setMap, this.getPanes() will return null.
    if (Map == null) {
        var Panes = this.getPanes();
        if (Panes)
            Panes.mapPane.removeChild(this.div_);

        if (/** @type {missouristate.web.TileEvents} */this.map.missouristate$web$tileEvents)
            this.map.missouristate$web$tileEvents.removeOverlay(this.overlayIndex);
    }
    else {
        /** @type {missouristate.web.TileEvents} */
        Map.missouristate$web$tileEvents = Map.missouristate$web$tileEvents || new missouristate.web.TileEvents();
        this.overlayIndex = Map.missouristate$web$tileEvents.addOverlay(this);
    }

    google.maps.OverlayView.prototype.setMap.call(this, Map);
}

/**
* @override
* @this {missouristate.web.TileOverlay}
*/
missouristate.web.TileOverlay.prototype['onRemove'] = function() {
    this.removeAllTiles();
    this.div_ = null;
}

/**
* @nosideeffects
* @this {missouristate.web.TileOverlay}
* @returns {boolean}
*/
missouristate.web.TileOverlay.prototype.getVisible = function() {
    return this.visible;
}

/**
* @param {boolean} Visible
* @this {missouristate.web.TileOverlay}
* @returns {boolean}
*/
missouristate.web.TileOverlay.prototype.setVisible = function(Visible) {
    if (this.div_) {
        if (Visible)
            this.div_.style.display = "block";
        else
            this.div_.style.display = "none";
    }

    this.visible = Visible;
}

missouristate.web.TileOverlay.prototype.setOpacity=function(opacity)
{
 if (opacity < 0)
 {
  opacity = 0 ;
 }
 if(opacity > 100)
 {
  opacity = 100 ;
 }
 var c = opacity/100 ;

 if (typeof(this.div_.style.filter) =='string')
 {
  this.div_.style.filter = 'alpha(opacity:' + opacity + ')' ;
 }
 if (typeof(this.div_.style.KHTMLOpacity) == 'string' )
 {
  this.div_.style.KHTMLOpacity = c ;
 }
 if (typeof(this.div_.style.MozOpacity) == 'string')
 {
  this.div_.style.MozOpacity = c ;
 }
 if (typeof(this.div_.style.opacity) == 'string')
 {
  this.div_.style.opacity = c ;
 }
}

