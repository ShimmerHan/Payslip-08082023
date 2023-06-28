/*
File Desc: EWS Base shared commonly used javascript functions
File Version : 20171204
*/
/// <reference path="jquery.min.js" />
/// <reference path="kendo.all.min.js" />
/// <reference path="../styles/kendo.material.min.css" />
/// <reference path="../styles/kendo.common.min.css" />
/// <reference path="loadingoverlay.min.js" />


var Base64={_keyStr:"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=",encode:function(e){var t="";var n,r,i,s,o,u,a;var f=0;e=Base64._utf8_encode(e);while(f<e.length){n=e.charCodeAt(f++);r=e.charCodeAt(f++);i=e.charCodeAt(f++);s=n>>2;o=(n&3)<<4|r>>4;u=(r&15)<<2|i>>6;a=i&63;if(isNaN(r)){u=a=64}else if(isNaN(i)){a=64}t=t+this._keyStr.charAt(s)+this._keyStr.charAt(o)+this._keyStr.charAt(u)+this._keyStr.charAt(a)}return t},decode:function(e){var t="";var n,r,i;var s,o,u,a;var f=0;e=e.replace(/[^A-Za-z0-9+/=]/g,"");while(f<e.length){s=this._keyStr.indexOf(e.charAt(f++));o=this._keyStr.indexOf(e.charAt(f++));u=this._keyStr.indexOf(e.charAt(f++));a=this._keyStr.indexOf(e.charAt(f++));n=s<<2|o>>4;r=(o&15)<<4|u>>2;i=(u&3)<<6|a;t=t+String.fromCharCode(n);if(u!=64){t=t+String.fromCharCode(r)}if(a!=64){t=t+String.fromCharCode(i)}}t=Base64._utf8_decode(t);return t},_utf8_encode:function(e){e=e.replace(/rn/g,"n");var t="";for(var n=0;n<e.length;n++){var r=e.charCodeAt(n);if(r<128){t+=String.fromCharCode(r)}else if(r>127&&r<2048){t+=String.fromCharCode(r>>6|192);t+=String.fromCharCode(r&63|128)}else{t+=String.fromCharCode(r>>12|224);t+=String.fromCharCode(r>>6&63|128);t+=String.fromCharCode(r&63|128)}}return t},_utf8_decode:function(e){var t="";var n=0;var r=c1=c2=0;while(n<e.length){r=e.charCodeAt(n);if(r<128){t+=String.fromCharCode(r);n++}else if(r>191&&r<224){c2=e.charCodeAt(n+1);t+=String.fromCharCode((r&31)<<6|c2&63);n+=2}else{c2=e.charCodeAt(n+1);c3=e.charCodeAt(n+2);t+=String.fromCharCode((r&15)<<12|(c2&63)<<6|c3&63);n+=3}}return t}};

function encodeStringParameter(strParameter) {
    var encodedString = Base64.encode(strParameter);
    return encodedString;
}

function decodeStringParameter(strParameter) {
    var decodedString = Base64.decode(strParameter);
    return decodedString;
}

//TODO: make it generic and reusable
var AjaxGlobalHandler = {
    Initiate: function (options) {
        $.ajaxSetup({ cache: false });
    
        // Ajax events fire in following order
        $(document).ajaxStart(function () {
//            $.blockUI({
//                message: options.AjaxWait.AjaxWaitMessage,
//                css: options.AjaxWait.AjaxWaitMessageCss
//            });
        }).ajaxSend(function (e, xhr, opts) {
        }).ajaxError(function (e, xhr, opts) {
            if (options.SessionOut.StatusCode == xhr.status) {
                window.location.replace(options.SessionOut.RedirectUrl);
                return;
            }
    
            //$.colorbox({ html: options.AjaxErrorMessage });
        }).ajaxSuccess(function (e, xhr, opts) {
        try{
            var response = JSON.parse(xhr.responseText)

            if(response.status == options.SessionOut.StatusCode)
            {
                //alert("expired");
                window.top.$('<div />').kendoWindow({ title: "Warning", resizable: false, modal: true, width: 500, draggable: false,
                        close: function () {
                            this.destroy();
                            window.close();
                window.top.location.replace(options.SessionOut.RedirectUrl);
                        }
                    }).data('kendoWindow').content("Invalid session. Please login again.").center().open();
                return;
            }}
            catch(err){

            }

//            if (options.SessionOut.StatusCode == xhr.status) {
//                document.location.replace(options.SessionOut.RedirectUrl);
//                return;
//            }
        }).ajaxComplete(function (e, xhr, opts) {
        }).ajaxStop(function () {
            //$.unblockUI();
        });
    }
};

//Default Ajax options. Can be override at each html that use base.js
var AjaxOptions = {
            AjaxWait: {
                AjaxWaitMessage: "<img style='height: 40px' src='' />",
                AjaxWaitMessageCss: { width: "50px", left: "45%" }
            },
            AjaxErrorMessage: "Unexpected error encountered. Please contact administrator if the problem persists.",
            SessionOut: {
                StatusCode: "9",
                RedirectUrl: "login.html"
            }
        };