﻿var debug_outputs = [];

function output(msg) {
	debug_outputs.push(msg.toString());
}

function LoadDocument(id) {
	var data = LoadDocumentInternal(id);
	return JSON.parse(data);
}

Array.prototype.Remove = function (val /*, thisp*/) {

	var res = this.indexOf(val);
	if (res == -1)
		return;
	this.splice(res, 1);

	return this;
};

Array.prototype.Where = Array.prototype.filter;

Array.prototype.RemoveWhere = function (fun /*, thisp*/) {
	var len = this.length;
	if (typeof fun != "function")
		throw new TypeError();

	var res = new Array();
	var thisp = arguments[1];
	for (var i = 0; i < len; i++) {
		if (i in this) {
			var val = this[i]; // in case fun mutates this
			if (fun.call(thisp, val, i, this)) {
				res.push(i);
			}
		}
	}

	for (var j = res.length -1; j >= 0; j--) {
		this.splice(res[j], 1);
	}

	return this;
}